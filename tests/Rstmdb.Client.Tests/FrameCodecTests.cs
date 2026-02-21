using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Rstmdb.Client.Protocol;
using Xunit;

namespace Rstmdb.Client.Tests;

public class FrameCodecTests
{
    [Fact]
    public void EncodeFrame_SetsCorrectMagic()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var frame = FrameCodec.EncodeFrame(payload);

        Assert.Equal((byte)'R', frame[0]);
        Assert.Equal((byte)'C', frame[1]);
        Assert.Equal((byte)'P', frame[2]);
        Assert.Equal((byte)'X', frame[3]);
    }

    [Fact]
    public void EncodeFrame_SetsProtocolVersion1()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var frame = FrameCodec.EncodeFrame(payload);

        var version = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4, 2));
        Assert.Equal((ushort)1, version);
    }

    [Fact]
    public void EncodeFrame_SetsCrcPresentFlag()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var frame = FrameCodec.EncodeFrame(payload);

        var flags = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(6, 2));
        Assert.Equal(FrameCodec.FlagCrcPresent, flags);
    }

    [Fact]
    public void EncodeFrame_SetsCorrectPayloadLength()
    {
        var payload = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
        var frame = FrameCodec.EncodeFrame(payload);

        var length = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(10, 4));
        Assert.Equal((uint)payload.Length, length);
    }

    [Fact]
    public void EncodeFrame_PayloadAppendedAfterHeader()
    {
        var payload = Encoding.UTF8.GetBytes("{\"test\":true}");
        var frame = FrameCodec.EncodeFrame(payload);

        Assert.Equal(FrameCodec.HeaderSize + payload.Length, frame.Length);
        Assert.Equal(payload, frame[FrameCodec.HeaderSize..]);
    }

    [Fact]
    public async Task EncodeDecodeRoundtrip()
    {
        var payload = Encoding.UTF8.GetBytes("{\"type\":\"request\",\"id\":\"1\",\"op\":\"PING\"}");
        var frame = FrameCodec.EncodeFrame(payload);

        using var stream = new MemoryStream(frame);
        var decoded = await FrameCodec.DecodeFrameAsync(stream, CancellationToken.None);

        Assert.Equal(payload, decoded);
    }

    [Fact]
    public async Task EncodeDecodeRoundtrip_EmptyPayload()
    {
        var payload = Array.Empty<byte>();
        var frame = FrameCodec.EncodeFrame(payload);

        using var stream = new MemoryStream(frame);
        var decoded = await FrameCodec.DecodeFrameAsync(stream, CancellationToken.None);

        Assert.Empty(decoded);
    }

    [Fact]
    public async Task EncodeDecodeRoundtrip_LargePayload()
    {
        var payload = new byte[1024 * 1024]; // 1 MiB
        new Random(42).NextBytes(payload);
        var frame = FrameCodec.EncodeFrame(payload);

        using var stream = new MemoryStream(frame);
        var decoded = await FrameCodec.DecodeFrameAsync(stream, CancellationToken.None);

        Assert.Equal(payload, decoded);
    }

    [Fact]
    public async Task DecodeFrame_InvalidMagic_Throws()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var frame = FrameCodec.EncodeFrame(payload);
        frame[0] = (byte)'X'; // corrupt magic

        using var stream = new MemoryStream(frame);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FrameCodec.DecodeFrameAsync(stream, CancellationToken.None));
        Assert.Contains("invalid frame magic", ex.Message);
    }

    [Fact]
    public async Task DecodeFrame_UnsupportedVersion_Throws()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var frame = FrameCodec.EncodeFrame(payload);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4, 2), 99); // bad version

        using var stream = new MemoryStream(frame);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FrameCodec.DecodeFrameAsync(stream, CancellationToken.None));
        Assert.Contains("unsupported protocol version", ex.Message);
    }

    [Fact]
    public async Task DecodeFrame_InvalidFlags_Throws()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var frame = FrameCodec.EncodeFrame(payload);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(6, 2), 0xFF00); // invalid flags

        using var stream = new MemoryStream(frame);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FrameCodec.DecodeFrameAsync(stream, CancellationToken.None));
        Assert.Contains("invalid frame flags", ex.Message);
    }

    [Fact]
    public async Task DecodeFrame_CrcMismatch_Throws()
    {
        var payload = Encoding.UTF8.GetBytes("{\"data\":\"test\"}");
        var frame = FrameCodec.EncodeFrame(payload);
        // Corrupt the CRC
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(14, 4), 0xDEADBEEF);

        using var stream = new MemoryStream(frame);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FrameCodec.DecodeFrameAsync(stream, CancellationToken.None));
        Assert.Contains("CRC32C checksum mismatch", ex.Message);
    }

    [Fact]
    public async Task DecodeFrame_OversizedPayload_Throws()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var frame = FrameCodec.EncodeFrame(payload);
        // Set payload length to exceed max
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(10, 4), (uint)(FrameCodec.MaxPayloadSize + 1));

        using var stream = new MemoryStream(frame);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FrameCodec.DecodeFrameAsync(stream, CancellationToken.None));
        Assert.Contains("MAX_PAYLOAD_SIZE", ex.Message);
    }

    [Fact]
    public void EncodeFrame_OversizedPayload_Throws()
    {
        var payload = new byte[FrameCodec.MaxPayloadSize + 1];
        var ex = Assert.Throws<InvalidOperationException>(() => FrameCodec.EncodeFrame(payload));
        Assert.Contains("MAX_PAYLOAD_SIZE", ex.Message);
    }

    [Fact]
    public async Task DecodeFrame_TruncatedStream_ThrowsIoException()
    {
        // Only 10 bytes, but header needs 18
        var data = new byte[10];
        using var stream = new MemoryStream(data);

        await Assert.ThrowsAsync<IOException>(
            () => FrameCodec.DecodeFrameAsync(stream, CancellationToken.None));
    }

    [Fact]
    public void Crc32C_KnownTestVector()
    {
        // Standard CRC32C test: crc32c("123456789") = 0xE3069283
        var data = Encoding.ASCII.GetBytes("123456789");
        var crc = Crc32Castagnoli.Compute(data);
        Assert.Equal(0xE3069283u, crc);
    }

    [Fact]
    public void Crc32C_EmptyData()
    {
        var crc = Crc32Castagnoli.Compute(Array.Empty<byte>());
        Assert.Equal(0x00000000u, crc);
    }

    [Fact]
    public void StringOrArray_DeserializeString()
    {
        var json = """{"from":"created","event":"PAY","to":"paid"}""";
        var transition = JsonSerializer.Deserialize<Transition>(json)!;

        Assert.Single(transition.From);
        Assert.Equal("created", transition.From[0]);
    }

    [Fact]
    public void StringOrArray_DeserializeArray()
    {
        var json = """{"from":["created","pending"],"event":"PAY","to":"paid"}""";
        var transition = JsonSerializer.Deserialize<Transition>(json)!;

        Assert.Equal(2, transition.From.Length);
        Assert.Equal("created", transition.From[0]);
        Assert.Equal("pending", transition.From[1]);
    }

    [Fact]
    public void StringOrArray_SerializeSingleAsString()
    {
        var transition = new Transition
        {
            From = new[] { "created" },
            Event = "PAY",
            To = "paid",
        };

        var json = JsonSerializer.Serialize(transition);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("from").ValueKind);
        Assert.Equal("created", doc.RootElement.GetProperty("from").GetString());
    }

    [Fact]
    public void StringOrArray_SerializeMultipleAsArray()
    {
        var transition = new Transition
        {
            From = new[] { "created", "pending" },
            Event = "PAY",
            To = "paid",
        };

        var json = JsonSerializer.Serialize(transition);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("from").ValueKind);
    }

    [Fact]
    public void BatchMode_SerializeAsSnakeCase()
    {
        var atomic = JsonSerializer.Serialize(BatchMode.Atomic);
        var bestEffort = JsonSerializer.Serialize(BatchMode.BestEffort);

        Assert.Equal("\"atomic\"", atomic);
        Assert.Equal("\"best_effort\"", bestEffort);
    }

    [Fact]
    public void BatchMode_DeserializeFromSnakeCase()
    {
        var atomic = JsonSerializer.Deserialize<BatchMode>("\"atomic\"");
        var bestEffort = JsonSerializer.Deserialize<BatchMode>("\"best_effort\"");

        Assert.Equal(BatchMode.Atomic, atomic);
        Assert.Equal(BatchMode.BestEffort, bestEffort);
    }
}
