using System.Buffers.Binary;
using System.Text;

namespace Rstmdb.Client.Protocol;

internal static class FrameCodec
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RCPX");
    public const int HeaderSize = 18;
    public const ushort ProtocolVersion = 1;
    public const int MaxPayloadSize = 16 * 1024 * 1024; // 16 MiB

    public const ushort FlagCrcPresent = 0x0001;
    public const ushort FlagCompressed = 0x0002;
    public const ushort FlagStream = 0x0004;
    public const ushort FlagEndStream = 0x0008;
    public const ushort FlagValidMask = 0x000F;

    public static byte[] EncodeFrame(byte[] payload)
    {
        if (payload.Length > MaxPayloadSize)
            throw new InvalidOperationException("rstmdb: frame exceeds MAX_PAYLOAD_SIZE (16 MiB)");

        var frame = new byte[HeaderSize + payload.Length];

        // Magic (4 bytes)
        Magic.CopyTo(frame.AsSpan(0, 4));
        // Version (2 bytes, BE)
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4, 2), ProtocolVersion);
        // Flags (2 bytes, BE) - CRC_PRESENT always set
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(6, 2), FlagCrcPresent);
        // Header extension length (2 bytes, BE) - always 0
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8, 2), 0);
        // Payload length (4 bytes, BE)
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(10, 4), (uint)payload.Length);
        // CRC32C (4 bytes, BE)
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(14, 4), Crc32Castagnoli.Compute(payload));
        // Payload
        payload.CopyTo(frame.AsSpan(HeaderSize));

        return frame;
    }

    public static async Task<byte[]> DecodeFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[HeaderSize];
        await ReadExactlyAsync(stream, header, HeaderSize, ct).ConfigureAwait(false);

        // Validate magic
        if (header[0] != Magic[0] || header[1] != Magic[1] ||
            header[2] != Magic[2] || header[3] != Magic[3])
            throw new InvalidOperationException("rstmdb: invalid frame magic (expected RCPX)");

        // Validate version
        var version = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        if (version != ProtocolVersion)
            throw new InvalidOperationException("rstmdb: unsupported protocol version");

        // Validate flags
        var flags = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(6, 2));
        if ((flags & ~FlagValidMask) != 0)
            throw new InvalidOperationException("rstmdb: invalid frame flags");

        // Header extension length
        var headerExtLen = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(8, 2));

        // Payload length
        var payloadLen = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(10, 4));
        if (payloadLen > MaxPayloadSize)
            throw new InvalidOperationException("rstmdb: frame exceeds MAX_PAYLOAD_SIZE (16 MiB)");

        // CRC from header
        var frameCrc = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(14, 4));

        // Skip header extension if present
        if (headerExtLen > 0)
        {
            var ext = new byte[headerExtLen];
            await ReadExactlyAsync(stream, ext, headerExtLen, ct).ConfigureAwait(false);
        }

        // Read payload
        var payload = new byte[payloadLen];
        if (payloadLen > 0)
        {
            await ReadExactlyAsync(stream, payload, (int)payloadLen, ct).ConfigureAwait(false);
        }

        // Validate CRC if present
        if ((flags & FlagCrcPresent) != 0)
        {
            var computed = Crc32Castagnoli.Compute(payload);
            if (computed != frameCrc)
                throw new InvalidOperationException("rstmdb: CRC32C checksum mismatch");
        }

        return payload;
    }

    internal static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct).ConfigureAwait(false);
            if (read == 0)
                throw new IOException("rstmdb: connection closed");
            offset += read;
        }
    }
}

/// <summary>
/// CRC32C (Castagnoli) implementation using polynomial 0x1EDC6F41.
/// </summary>
internal static class Crc32Castagnoli
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        const uint polynomial = 0x82F63B78; // reversed Castagnoli polynomial
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ polynomial;
                else
                    crc >>= 1;
            }
            table[i] = crc;
        }
        return table;
    }

    public static uint Compute(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFF;
    }
}
