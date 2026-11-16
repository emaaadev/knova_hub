using KnovaHub.ApplicationLayer.Contract;
using KnovaHub.ApplicationLayer.DTOs;
using KnovaHub.DomainLayer.Entities;
using KnovaHub.DomainLayer.Exceptions;
using KnovaHub.DomainLayer.Repository;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KnovaHub.ApplicationLayer.Services;

public class QueryService : IQueryService
{
    private readonly IDocumentRepository _documents;
    private readonly IAiService _iaServices;
    private readonly IQueryLogRepository _queryLog;
    private readonly IDepartmentRepository _departments;
    private readonly IUserRepository _users;

    public QueryService(
        IDocumentRepository documents,
        IAiService iaServices,
        IQueryLogRepository queryLog,
        IDepartmentRepository departments,
        IUserRepository users)
    {
        _documents = documents;
        _iaServices = iaServices;
        _queryLog = queryLog;
        _departments = departments;
        _users = users;
    }

    private static int GetCompanyId(Actor actor)
    {
        if (actor.CompanyId is not int companyId || companyId <= 0)
            throw new DataValidationException("El usuario no tiene una empresa válida.");

        return companyId;
    }

    private async Task<User> GetUser(Actor actor)
    {
        var user = await _users.GetByIdInCompanyAsync(actor.CompanyId!.Value, actor.UserId);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("El usuario no está activo o no pertenece a la empresa.");
        }
        return user;
    }

    private async Task<int[]?> GetVisibleDepartmentIds(Actor actor, int companyId, User user)
    {
        if (actor.Role == SystemRoles.AdminEmpresaRoleName)
        {
            return null;
        }

        if (actor.Role == SystemRoles.SupervisorRoleName)
        {
            var supervised = await _departments.GetSupervisedAsync(actor.UserId);
            return supervised
                .Where(d => d.CompanyId == companyId)
                .Select(d => d.Id)
                .ToArray();
        }

        return user.DepartmentId is int deptId ? new[] { deptId } : Array.Empty<int>();
    }

    public async Task<AiAskResponseDto> AskAsync(Actor actor, string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new DataValidationException("La pregunta no puede estar vacía.");
        }

        int companyId = GetCompanyId(actor);
        var user = await GetUser(actor);
        var visibleDepartmentIds = await GetVisibleDepartmentIds(actor, companyId, user);

        var indexedDocuments = await _documents.GetIndexedTextsAsync(companyId, visibleDepartmentIds);

        var context = indexedDocuments
            .Select(d => new AiContextChunkDto
            {
                DocumentId = d.DocumentId,
                Title = d.Title,
                Content = d.Markdown
            })
            .ToList();

        var request = new AiRequestDto
        {
            UserId = actor.UserId,
            CompanyId = companyId,
            Question = question,
            Context = context
        };

        string? answer = null;
        try
        {
            var aiResponse = await _iaServices.AskAsync(request);
            answer = aiResponse.Answer;
        }
        finally
        {
            var log = new QueryLog
            {
                UserId = actor.UserId,
                CompanyId = companyId,
                Question = question,
                DocumentIds = string.Join(",", indexedDocuments.Select(d => d.DocumentId)),
                AskedAt = DateTime.UtcNow
            };
            await _queryLog.AddAsync(log);
        }

        return new AiAskResponseDto
        {
            Answer = answer ?? string.Empty,
            Sources = indexedDocuments.Select(d => new AiSourceDto
            {
                DocumentId = d.DocumentId,
                Title = d.Title
            }).ToList()
        };
    }
}
