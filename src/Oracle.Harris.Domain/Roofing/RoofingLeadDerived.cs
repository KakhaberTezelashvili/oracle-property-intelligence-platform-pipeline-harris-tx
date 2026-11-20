namespace Oracle.Harris.Domain.Roofing;

public sealed record RoofingLeadDerived
{
    public required string RequestIdentifier { get; init; }

    public int? RoofAgeYears { get; init; }

    public int? DaysOpen { get; init; }

    public bool IsOutOfAreaOwner { get; init; }

    public int? YearsSinceSale { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? ContractorName { get; init; }

    public string? BbbRating { get; init; }
}