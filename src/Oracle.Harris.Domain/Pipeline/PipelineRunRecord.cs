namespace Oracle.Harris.Domain.Pipeline;

public sealed record PipelineRunRecord
{
    public required string RunId { get; init; }

    public required string CountySlug { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string SourcesJson { get; init; } = "[]";

    public string CountsJson { get; init; } = "{}";

    public string DeltasJson { get; init; } = "{}";

    public string? SourceLimitations { get; init; }

    public string Status { get; init; } = "running";
}