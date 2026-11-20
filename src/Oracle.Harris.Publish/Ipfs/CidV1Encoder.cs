using System.Security.Cryptography;

namespace Oracle.Harris.Publish.Ipfs;

/// <summary>CIDv1 with multibase base32btc (prefix <c>b</c>).</summary>
public static class CidV1Encoder
{
    private const byte RawCodec = 0x55;
    private const byte Sha256Code = 0x12;

    public static string EncodeFile(ReadOnlySpan<byte> content)
    {
        var hash = SHA256.HashData(content);
        return EncodeFromDigest(hash, RawCodec);
    }

    public static string EncodeFromDigest(ReadOnlySpan<byte> digest, byte codec)
    {
        if (digest.Length != 32)
        {
            throw new ArgumentException("SHA-256 digest must be 32 bytes.", nameof(digest));
        }

        var multihash = new byte[34];
        multihash[0] = Sha256Code;
        multihash[1] = 32;
        digest.CopyTo(multihash.AsSpan(2));

        using var ms = new MemoryStream();
        ms.WriteByte(0x01);
        WriteVarint(ms, codec);
        ms.Write(multihash, 0, multihash.Length);

        var base32 = Base32.Encode(ms.ToArray());
        return "b" + base32;
    }

    public static string Sha256Hex(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static void WriteVarint(Stream stream, ulong value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }

    private static class Base32
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

        public static string Encode(byte[] data)
        {
            var output = new char[(int)Math.Ceiling(data.Length / 5d) * 8];
            var buffer = 0;
            var bitsLeft = 0;
            var index = 0;

            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;

                while (bitsLeft >= 5)
                {
                    var val = (buffer >> (bitsLeft - 5)) & 0x1F;
                    bitsLeft -= 5;
                    output[index++] = Alphabet[val];
                }
            }

            if (bitsLeft > 0)
            {
                var val = (buffer << (5 - bitsLeft)) & 0x1F;
                output[index++] = Alphabet[val];
            }

            return new string(output, 0, index);
        }
    }
}