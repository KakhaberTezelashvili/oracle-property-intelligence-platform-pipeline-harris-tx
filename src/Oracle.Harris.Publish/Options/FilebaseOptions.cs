namespace Oracle.Harris.Publish.Options;

public sealed class FilebaseOptions
{
    public const string SectionName = "Filebase";

    public string Endpoint { get; set; } = "https://s3.filebase.io";

    public string Bucket { get; set; } = "elephant-oracle-query-table-harris-tx";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string IpnsLabel { get; set; } = "oracle-query-table-harris-tx";

    public string Gateway1 { get; set; } = "https://ipfs.io/ipfs/";

    public string Gateway2 { get; set; } = "https://dweb.link/ipfs/";
}