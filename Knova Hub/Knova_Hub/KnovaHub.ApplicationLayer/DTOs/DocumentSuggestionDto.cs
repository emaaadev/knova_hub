using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.ApplicationLayer.DTOs;

public record DocumentSuggestionDto(
    int Id,
    string Title,
    string? Description,
    DocumentType Type,
    int? CategoryId);
