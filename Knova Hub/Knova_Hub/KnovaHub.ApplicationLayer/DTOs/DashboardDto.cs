using KnovaHub.DomainLayer.Entities;

namespace KnovaHub.ApplicationLayer.DTOs;

public record DashboardDto(
    int Documents,
    int Wikis,
    int Minutes,
    int? Users,
    IReadOnlyList<DashboardActivityDto> RecentActivity);

public record DashboardActivityDto(
    int Id,
    string Title,
    DocumentType Type,
    DateTime OccurredAt,
    bool Updated);
