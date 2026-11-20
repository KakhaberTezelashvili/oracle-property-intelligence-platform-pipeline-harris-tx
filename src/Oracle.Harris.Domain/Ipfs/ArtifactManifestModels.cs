namespace Oracle.Harris.Domain.Ipfs;

public sealed record ArtifactManifestEntry
{
    public required string Cid { get; init; }

    public required string Name { get; init; }

    public long SizeBytes { get; init; }

    public required string Codec { get; init; }

    public required string Sha256 { get; init; }

    public IReadOnlyList<string>? Origins { get; init; }
}

public sealed record PipelineRunManifest
{
    public required string RunId { get; init; }

    public required string CountySlug { get; init; }

    public DateTimeOffset PublishedAt { get; init; }

    public string? IpnsName { get; init; }

    public string? IpnsResolvedCid { get; init; }

    public IReadOnlyList<ArtifactManifestEntry> Artifacts { get; init; } =
        Array.Empty<ArtifactManifestEntry>();
}

public sealed record GatewayVerificationResult
{
    public required string GatewayBaseUrl { get; init; }

    public required string Cid { get; init; }

    public bool Success { get; init; }

    public long ByteLength { get; init; }

    public string? Sha256 { get; init; }

    public string? Error { get; init; }
}