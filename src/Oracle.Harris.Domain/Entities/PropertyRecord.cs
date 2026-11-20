namespace Oracle.Harris.Domain.Entities;

public sealed record PropertyRecord
{
    public required string RequestIdentifier { get; init; }

    public string? PropertyUsageType { get; init; }

    public int? YearBuilt { get; init; }

    public int? RoofYear { get; init; }

    public string? RoofCoveringMaterial { get; init; }

    public string? ExteriorWallMaterial { get; init; }

    public double? LotAreaSqft { get; init; }

    public double? LotSizeAcre { get; init; }
}