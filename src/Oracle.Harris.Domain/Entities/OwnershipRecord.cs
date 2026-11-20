namespace Oracle.Harris.Domain.Entities;

public sealed record OwnershipRecord
{
    public required string RequestIdentifier { get; init; }

    public string? OwnerName { get; init; }

    public string? MailingCity { get; init; }

    public string? MailingState { get; init; }

    public string? MailingPostalCode { get; init; }

    public string? SitusState { get; init; }

    public DateOnly? LastSaleDate { get; init; }
}