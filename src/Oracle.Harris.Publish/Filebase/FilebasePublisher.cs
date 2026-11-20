using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Oracle.Harris.Publish.Options;

namespace Oracle.Harris.Publish.Filebase;

public sealed class FilebasePublisher(IOptions<FilebaseOptions> options)
{
    private readonly FilebaseOptions _options = options.Value;

    public async Task UploadFileAsync(string key, Stream content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessKey) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            // Scaffold: skip remote upload when credentials are absent
            return;
        }

        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true,
        };
        using var client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
        };
        await client.PutObjectAsync(request, cancellationToken);
    }
}