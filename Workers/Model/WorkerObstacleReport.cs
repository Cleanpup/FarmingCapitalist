namespace HireSkilledHelpers.Workers;

internal sealed class WorkerObstacleReport
{
    public string Id { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public WorkerTaskKind Task { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public WorkerObstacleReport Clone() => new()
    {
        Id = this.Id,
        Message = this.Message,
        Task = this.Task,
        LocationName = this.LocationName,
    };
}
