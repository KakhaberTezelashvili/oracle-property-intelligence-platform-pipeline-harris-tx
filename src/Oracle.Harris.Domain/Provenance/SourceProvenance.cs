namespace Oracle.Harris.Domain.Provenance;

public sealed record SourceProvenance
{
    public required string SourceSystem { get; init; }

    public required string SourceRecordKey { get; init; }

    public string? SourceArtifactUri { get; init; }

    public DateTimeOffset? SourceCollectedAt { get; init; }

    public string? SourcePayloadJson { get; init; }

    public string? ContentHash { get; init; }
}