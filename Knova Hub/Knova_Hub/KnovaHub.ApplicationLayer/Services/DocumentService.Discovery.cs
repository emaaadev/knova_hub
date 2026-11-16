using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;

namespace KnovaHub.ApplicationLayer.Services;

public partial class DocumentService
{
    private const int SuggestionLimit = 10;
    private const int HistoryLimit = 10;

    private readonly IQueryLogRepository _queryLogs;
    private readonly TimeProvider _clock;

    private static readonly Regex Words = new(@"[\p{L}\p{Nd}]{3,}", RegexOptions.NonBacktracking);
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "como", "cual", "cuando", "del", "desde", "donde", "esta", "este", "estos", "hay",
        "las", "los", "para", "por", "puede", "que", "sin", "sobre", "son", "una", "uno",
        "and", "are", "for", "from", "how", "the", "this", "what", "with"
    };

    public async Task<List<DocumentSuggestionDto>> GetRelatedAsync(
        Actor actor, int documentId, CancellationToken cancellationToken)
    {
        int companyId = GetCompanyId(actor);
        var departments = await GetVisibleDepartmentIdsAsync(actor, companyId);
        var documents = await _documents.GetDiscoveryDocumentsAsync(
            companyId, departments, includeReadCounts: false, cancellationToken);

        var source = documents.Find(document => document.Id == documentId)
            ?? throw new NotFoundException("El documento no existe o no tienes acceso.");

        var sourceTerms = GetDocumentTerms(source);
        return documents.Where(document => document.Id != source.Id)
            .Select(document => (Document: document,
                Score: RelatedScore(document, source, GetDocumentTerms(document), sourceTerms)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Document.ActivityAt)
            .ThenBy(item => item.Document.Id)
            .Take(SuggestionLimit)
            .Select(item => ToSuggestion(item.Document))
            .ToList();
    }

    public async Task<List<DocumentSuggestionDto>> GetRecommendationsAsync(
        Actor actor, CancellationToken cancellationToken)
    {
        int companyId = GetCompanyId(actor);
        var departments = await GetVisibleDepartmentIdsAsync(actor, companyId);
        var documents = await _documents.GetDiscoveryDocumentsAsync(
            companyId, departments, includeReadCounts: true, cancellationToken);

        var since = _clock.GetUtcNow().UtcDateTime.AddDays(-30);
        var questionHistory = await _queryLogs.GetRecentQuestionsAsync(
            companyId, actor.UserId, since, HistoryLimit, cancellationToken);
        var recentReadIds = (await _documents.GetRecentlyReadIdsAsync(
            companyId, actor.UserId, since, HistoryLimit, cancellationToken)).ToHashSet();

        var questionTerms = GetTerms(questionHistory.ToArray());
        var recentDocuments = documents.Where(document => recentReadIds.Contains(document.Id))
            .Select(document => (Document: document, Terms: GetDocumentTerms(document)))
            .ToArray();

        return documents
            .Select(document =>
            {
                if (recentReadIds.Contains(document.Id) ||
                    (questionTerms.Count == 0 && recentDocuments.Length == 0))
                    return (Document: document, Score: 0d);

                var terms = GetDocumentTerms(document);
                var recentScore = recentDocuments
                    .Select(recent => RelatedScore(document, recent.Document, terms, recent.Terms))
                    .DefaultIfEmpty()
                    .Max();

                return (Document: document, Score: recentScore + 6 * Similarity(terms, questionTerms));
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Document.ReadCount)
            .ThenByDescending(item => item.Document.ActivityAt)
            .ThenBy(item => item.Document.Id)
            .Take(SuggestionLimit)
            .Select(item => ToSuggestion(item.Document))
            .ToList();
    }

    private static double RelatedScore(
        DiscoveryDocument document, DiscoveryDocument source, HashSet<string> terms, HashSet<string> sourceTerms)
    {
        var sameCategory = document.CategoryId.HasValue && document.CategoryId == source.CategoryId;
        var sharedTags = document.TagIds.Intersect(source.TagIds).Count();
        return (sameCategory ? 3 : 0) + Math.Min(sharedTags, 3) * 2
            + 6 * Similarity(terms, sourceTerms);
    }

    private static double Similarity(HashSet<string> terms, HashSet<string> context)
    {
        if (terms.Count == 0 || context.Count == 0)
            return 0;

        var shared = context.Count(terms.Contains);
        return shared / Math.Sqrt((double)terms.Count * context.Count);
    }

    private static HashSet<string> GetDocumentTerms(DiscoveryDocument document) =>
        GetTerms(document.Title, document.Description, document.Content, document.Markdown);

    private static HashSet<string> GetTerms(params string?[] fragments)
    {
        var terms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fragment in fragments)
        {
            if (string.IsNullOrWhiteSpace(fragment))
                continue;

            var normalized = new string(fragment.Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .ToArray()).ToLowerInvariant();

            foreach (Match word in Words.Matches(normalized))
            {
                if (!StopWords.Contains(word.Value))
                    terms.Add(word.Value);
            }
        }

        return terms;
    }

    private static DocumentSuggestionDto ToSuggestion(DiscoveryDocument document) =>
        new(document.Id, document.Title, document.Description, document.Type, document.CategoryId);
}
