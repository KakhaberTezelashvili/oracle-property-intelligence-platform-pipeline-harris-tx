namespace Oracle.Harris.Domain.Entities;

public sealed record BusinessLocationRecord
{
    public required string GersId { get; init; }

    public string? TaxonomyPrimary { get; init; }

    public string? TaxonomyHierarchy { get; init; }

    public string? BasicCategory { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public required string OvertureRelease { get; init; }

    public string? SourcePayloadJson { get; init; }
}