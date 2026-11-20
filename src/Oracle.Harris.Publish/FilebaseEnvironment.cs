using Oracle.Harris.Publish.Options;

namespace Oracle.Harris.Publish;

/// <summary>
/// Binds Filebase/S3 options from process environment variables (Azure App Service, local User env, etc.).
/// </summary>
public static class FilebaseEnvironment
{
    public static void Apply(FilebaseOptions options)
    {
        options.Endpoint = FirstNonEmpty(
            Environment.GetEnvironmentVariable("S3_ENDPOINT"),
            Environment.GetEnvironmentVariable("FILEBASE_ENDPOINT"),
            Environment.GetEnvironmentVariable("ENDPOINT"))
            ?? options.Endpoint;

        options.AccessKey = FirstNonEmpty(
            Environment.GetEnvironmentVariable("S3_ACCESS_KEY_ID"),
            Environment.GetEnvironmentVariable("FILEBASE_ACCESS_KEY"),
            Environment.GetEnvironmentVariable("ACCESS_KEY"))
            ?? string.Empty;

        options.SecretKey = FirstNonEmpty(
            Environment.GetEnvironmentVariable("S3_SECRET_ACCESS_KEY"),
            Environment.GetEnvironmentVariable("FILEBASE_SECRET_KEY"),
            Environment.GetEnvironmentVariable("SECRET_KEY"))
            ?? string.Empty;

        options.Bucket = FirstNonEmpty(
            Environment.GetEnvironmentVariable("FILEBASE_QUERY_TABLE_BUCKET_HARRIS_TX"),
            Environment.GetEnvironmentVariable("FILEBASE_BUCKET"),
            Environment.GetEnvironmentVariable("S3_BUCKET"))
            ?? options.Bucket;

        options.IpnsLabel = FirstNonEmpty(
            Environment.GetEnvironmentVariable("FILEBASE_QUERY_TABLE_IPNS_LABEL"),
            Environment.GetEnvironmentVariable("FILEBASE_IPNS_LABEL"))
            ?? options.IpnsLabel;

        options.Gateway1 = FirstNonEmpty(Environment.GetEnvironmentVariable("IPFS_GATEWAY_1")) ?? options.Gateway1;
        options.Gateway2 = FirstNonEmpty(Environment.GetEnvironmentVariable("IPFS_GATEWAY_2")) ?? options.Gateway2;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}