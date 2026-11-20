using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.Harris.Domain.Ipfs;
using Oracle.Harris.Publish.Options;

namespace Oracle.Harris.Publish.Verification;

public sealed class DualGatewayVerifier(
    HttpClient httpClient,
    IOptions<FilebaseOptions> options,
    ILogger<DualGatewayVerifier> logger)
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(15)];

    private readonly FilebaseOptions _options = options.Value;

    public async Task<IReadOnlyList<GatewayVerificationResult>> VerifyAsync(
        string cid,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        var gateways = new[] { _options.Gateway1, _options.Gateway2 };
        var results = new List<GatewayVerificationResult>();
        foreach (var gateway in gateways)
        {
            results.Add(await VerifyOneAsync(gateway, cid, expectedSize, expectedSha256, cancellationToken));
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
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
        var url = $"{gatewayBase.TrimEnd('/')}/{cid}";
        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (ShouldRetry(response.StatusCode) && attempt < RetryDelays.Length)
                {
                    logger.LogWarning(
                        "Gateway {Gateway} returned {Status} for {Cid}; retry {Attempt}/{Max}",
                        gatewayBase,
                        (int)response.StatusCode,
                        cid,
                        attempt + 1,
                        RetryDelays.Length);
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Fail(gatewayBase, cid, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var sizeOk = bytes.LongLength == expectedSize;
                var shaOk = string.Equals(sha, expectedSha256, StringComparison.OrdinalIgnoreCase);
                return new GatewayVerificationResult
                {
                    GatewayBaseUrl = gatewayBase,
                    Cid = cid,
                    Success = sizeOk && shaOk,
                    ByteLength = bytes.LongLength,
                    Sha256 = sha,
                    Error = sizeOk && shaOk
                        ? null
                        : $"digest/size mismatch (got {bytes.LongLength} bytes, sha256={sha})",
                };
            }
            catch (Exception ex) when (attempt < RetryDelays.Length)
            {
                logger.LogWarning(ex, "Gateway fetch failed for {Cid}, retry {Attempt}", cid, attempt + 1);
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
            catch (Exception ex)
            {
                return Fail(gatewayBase, cid, ex.Message);
            }
        }

        return Fail(gatewayBase, cid, "Exhausted gateway retries");
    }

    private static bool ShouldRetry(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.BadGateway;

    private static GatewayVerificationResult Fail(string gatewayBase, string cid, string error) =>
        new()
        {
            GatewayBaseUrl = gatewayBase,
            Cid = cid,
            Success = false,
            Error = error,
        };
}