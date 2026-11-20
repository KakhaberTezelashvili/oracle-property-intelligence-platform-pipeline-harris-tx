namespace Oracle.Harris.Publish.Filebase;

public sealed record FilebaseUploadResult
{
  public bool Skipped { get; init; }

  /// <summary>UnixFS CID assigned by Filebase (public IPFS identity).</summary>
  public string? IpfsCid { get; init; }
}