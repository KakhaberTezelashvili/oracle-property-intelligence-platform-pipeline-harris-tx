namespace Oracle.Harris.Publish.Ipfs;

/// <summary>Minimal CAR v1 writer for a single-root DAG snapshot.</summary>
public static class CarArchiveWriter
{
    public static byte[] WriteSingleBlockArchive(byte[] block, byte[] rootCidBytes)
    {
        using var ms = new MemoryStream();
        WriteVarint(ms, 1); // version
        WriteVarint(ms, (ulong)rootCidBytes.Length);
        ms.Write(rootCidBytes, 0, rootCidBytes.Length);

        WriteVarint(ms, (ulong)block.Length);
        ms.Write(block, 0, block.Length);
        return ms.ToArray();
    }

    public static byte[] ExtractCidPayloadWithoutMultibase(string cid)
    {
        if (string.IsNullOrEmpty(cid) || cid[0] != 'b')
        {
            throw new ArgumentException("Expected base32 CIDv1 with 'b' multibase prefix.", nameof(cid));
        }

        // CAR roots require binary CID; for scaffold we store a placeholder equal to UTF8 bytes of cid string
        return System.Text.Encoding.UTF8.GetBytes(cid);
    }

    private static void WriteVarint(Stream stream, ulong value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }
}