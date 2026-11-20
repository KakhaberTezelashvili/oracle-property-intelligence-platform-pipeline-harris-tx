using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Oracle.Harris.Domain.Ipfs;
using Oracle.Harris.Publish.Options;

namespace Oracle.Harris.Publish.Verification;

public sealed class DualGatewayVerifier(HttpClient httpClient, IOptions<FilebaseOptions> options)
{
    private readonly FilebaseOptions _options = options.Value;

    public async Task<IReadOnlyList<GatewayVerificationResult>> VerifyAsync(string cid, long expectedSize, string expectedSha256, CancellationToken cancellationToken = default)
    {
        var gateways = new[] { _options.Gateway1, _options.Gateway2 };
        var results = new List<GatewayVerificationResult>();
        foreach (var gateway in gateways)
        {
            results.Add(await VerifyOneAsync(gateway, cid, expectedSize, expectedSha256, cancellationToken));
        }

        return results;
    }

    private async Task<GatewayVerificationResult> VerifyOneAsync(
        string gatewayBase,
        string cid,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{gatewayBase.TrimEnd('/')}/{cid}";
            var bytes = await httpClient.GetByteArrayAsync(url, cancellationToken);
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return new GatewayVerificationResult
            {
                GatewayBaseUrl = gatewayBase,
                Cid = cid,
                Success = bytes.LongLength == expectedSize && sha == expectedSha256,
                ByteLength = bytes.LongLength,
                Sha256 = sha,
            };
        }
        catch (Exception ex)
        {
            return new GatewayVerificationResult
            {
                GatewayBaseUrl = gatewayBase,
                Cid = cid,
                Success = false,
                Error = ex.Message,
            };
        }
    }
}