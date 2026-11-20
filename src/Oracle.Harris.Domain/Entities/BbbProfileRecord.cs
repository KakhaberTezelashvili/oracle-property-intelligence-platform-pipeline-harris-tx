namespace Oracle.Harris.Domain.Entities;

public sealed record BbbProfileRecord
{
    public required string ProfileKey { get; init; }

    public required string BusinessName { get; init; }

    public string? Rating { get; init; }

    public string? Category { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? SourcePayloadJson { get; init; }
}