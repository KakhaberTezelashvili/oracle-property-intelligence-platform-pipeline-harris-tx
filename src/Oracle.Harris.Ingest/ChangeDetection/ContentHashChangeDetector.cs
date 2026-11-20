using System.Security.Cryptography;
using System.Text;

namespace Oracle.Harris.Ingest.ChangeDetection;

public static class ContentHashChangeDetector
{
    public static string ComputeHash(ReadOnlySpan<byte> content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeHash(string content) =>
        ComputeHash(Encoding.UTF8.GetBytes(content));
}