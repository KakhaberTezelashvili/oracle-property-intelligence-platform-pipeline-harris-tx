namespace Oracle.Harris.Domain.Entities;

public sealed record GeometryRecord
{
    public required string RequestIdentifier { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}