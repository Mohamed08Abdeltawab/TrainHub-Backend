namespace TrainHub.Application.Features.Dashboard.DTOs;

/// <summary>
/// Dashboard payload consumed by the React SPA. Field names match
/// TrainHub-Frontend/src/types/nav.ts (DashboardContent) exactly.
/// </summary>
public record DashboardContentDto
{
    public string Tenant { get; init; } = string.Empty;
    public string Plan { get; init; } = string.Empty;
    public string Section { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public IReadOnlyList<DashboardMetricDto> Metrics { get; init; } = [];
    public string TableTitle { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public IReadOnlyList<DashboardRowDto> Rows { get; init; } = [];
    public string Scope { get; init; } = string.Empty;
    public IReadOnlyList<string> Capabilities { get; init; } = [];
}

public record DashboardMetricDto
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
}

public record DashboardRowDto
{
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
