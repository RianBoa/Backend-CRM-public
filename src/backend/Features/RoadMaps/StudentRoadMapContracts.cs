using System.ComponentModel.DataAnnotations;

namespace backend.Features.RoadMaps;

public sealed class StudentRoadMapQuery
{
    [Range(1, 1000000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [Range(1, int.MaxValue)] public int? TemplateId { get; init; }
    [RegularExpression("^(Pending|Completed|Overdue|Skipped)$")] public string? Status { get; init; }
    [StringLength(120)] public string? Search { get; init; }
    [RegularExpression("^(dueAt|title)$")] public string SortBy { get; init; } = "dueAt";
    [RegularExpression("^(asc|desc)$")] public string SortDir { get; init; } = "asc";
}

public sealed record CompletionRequest([Required] bool? Completed);
public sealed record StudentRoadMapItem(string AssignmentId, int ItemId, int TemplateId,
    string Title, string Type, string Status, bool Completed, DateTime DueAt);
public sealed record CompletionResult(StudentRoadMapItem? Item, string? ErrorCode = null);
