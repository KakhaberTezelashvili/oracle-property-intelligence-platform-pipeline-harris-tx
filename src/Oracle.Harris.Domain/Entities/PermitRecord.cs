namespace Oracle.Harris.Domain.Entities;

public sealed record PermitRecord
{
    public required string PermitNumber { get; init; }

    public required string SourceSystem { get; init; }

    public string? RequestIdentifier { get; init; }

    public string? PermitType { get; init; }

    public string? Status { get; init; }

    public DateOnly? IssuedAt { get; init; }

    public DateOnly? ClosedAt { get; init; }

    public int? DaysOpen { get; init; }

    public bool IsOpen { get; init; }

    public bool IsRoofingRelated { get; init; }

    public string? ContractorName { get; init; }

    public string? SourcePayloadJson { get; init; }
}