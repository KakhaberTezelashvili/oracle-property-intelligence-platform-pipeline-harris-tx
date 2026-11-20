using Oracle.Harris.Domain.Provenance;

namespace Oracle.Harris.Domain.Entities;

public sealed record ParcelRecord
{
    public required string JurisdictionKey { get; init; }

  /// <summary>Canonical folio/account — the 1:1 parcel key.</summary>
    public required string RequestIdentifier { get; init; }

    public string? ParcelIdentifier { get; init; }

    public required SourceProvenance Provenance { get; init; }
}