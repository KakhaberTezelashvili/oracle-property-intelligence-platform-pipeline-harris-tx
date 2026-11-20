using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.Harris.Publish.Options;

namespace Oracle.Harris.Publish.Filebase;

public sealed class FilebasePublisher(IOptions<FilebaseOptions> options, ILogger<FilebasePublisher> logger)
{
    private readonly FilebaseOptions _options = options.Value;

    public async Task<FilebaseUploadResult> UploadFileAsync(string key, Stream content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessKey) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            logger.LogWarning("Filebase credentials missing; skipping upload for {Key}", key);
            return new FilebaseUploadResult { Skipped = true };
        }

        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
        };
        using var client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
        };
        var putResponse = await client.PutObjectAsync(request, cancellationToken);

        var metadata = await client.GetObjectMetadataAsync(
            new GetObjectMetadataRequest
            {
                BucketName = _options.Bucket,
                Key = key,
            },
            cancellationToken);

        var cid = ReadIpfsCid(putResponse, metadata);
        if (string.IsNullOrWhiteSpace(cid))
        {
            var metaKeys = metadata.Metadata?.Keys is { Count: > 0 } keys
                ? string.Join(", ", keys)
                : "(none)";
            logger.LogWarning(
                "No IPFS CID for {Key} in bucket {Bucket}. Head metadata keys: {Keys}. " +
                "In Filebase, create an **IPFS** bucket (object-storage buckets do not return x-amz-meta-cid).",
                key,
                _options.Bucket,
                metaKeys);
        }
        else
        {
            logger.LogInformation("Pinned {Key} → IPFS CID {Cid}", key, cid);
        }

        return new FilebaseUploadResult { IpfsCid = cid };
    }

    private static string? ReadIpfsCid(PutObjectResponse putResponse, GetObjectMetadataResponse head)
    {
        if (putResponse.ResponseMetadata?.Metadata is { Count: > 0 } putMeta)
        {
            foreach (var key in putMeta.Keys)
            {
                if (key.Contains("cid", StringComparison.OrdinalIgnoreCase))
                {
                    return putMeta[key];
                }
            }
        }

        if (head.Metadata is null)
        {
            return null;
        }

        foreach (var key in head.Metadata.Keys)
        {
            if (key.Contains("cid", StringComparison.OrdinalIgnoreCase))
            {
                return head.Metadata[key];
            }
        }

        return null;
    }
}