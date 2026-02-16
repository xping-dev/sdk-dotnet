/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Tests.Serialization;

public sealed class XpingJsonSerializerTests
{
    private sealed record SampleRecord(string Name, int Value);

    private static IXpingSerializer BuildSerializer()
    {
        var services = new ServiceCollection();
        services.AddXpingSerialization();
        return services.BuildServiceProvider().GetRequiredService<IXpingSerializer>();
    }

    // ---------------------------------------------------------------------------
    // Serialize (string)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Serialize_ShouldReturnJson_ForSimpleObject()
    {
        var serializer = BuildSerializer();
        var obj = new SampleRecord("hello", 42);

        var json = serializer.Serialize(obj);

        Assert.Contains("hello", json, StringComparison.Ordinal);
        Assert.Contains("42", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_ShouldUseCamelCase_ByDefault()
    {
        var serializer = BuildSerializer();
        var obj = new SampleRecord("test", 1);

        var json = serializer.Serialize(obj);

        // ApiOptions uses camelCase naming policy
        Assert.Contains("\"name\"", json, StringComparison.Ordinal);
        Assert.Contains("\"value\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Name\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_Null_ShouldThrowArgumentNullException()
    {
        var serializer = BuildSerializer();
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize<SampleRecord>(null!));
    }

    // ---------------------------------------------------------------------------
    // SerializeToUtf8Bytes
    // ---------------------------------------------------------------------------

    [Fact]
    public void SerializeToUtf8Bytes_ShouldReturnBytes_ForObject()
    {
        var serializer = BuildSerializer();
        var obj = new SampleRecord("utf8", 99);

        var bytes = serializer.SerializeToUtf8Bytes(obj);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
        var json = Encoding.UTF8.GetString(bytes);
        Assert.Contains("99", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeToUtf8Bytes_Null_ShouldThrowArgumentNullException()
    {
        var serializer = BuildSerializer();
        Assert.Throws<ArgumentNullException>(() => serializer.SerializeToUtf8Bytes<SampleRecord>(null!));
    }

    // ---------------------------------------------------------------------------
    // Deserialize<T>(string)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deserialize_String_ShouldReturnObject()
    {
        var serializer = BuildSerializer();
        var json = "{\"name\":\"world\",\"value\":7}";

        var result = serializer.Deserialize<SampleRecord>(json);

        Assert.NotNull(result);
        Assert.Equal("world", result!.Name);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public void Deserialize_NullString_ShouldThrowArgumentNullException()
    {
        var serializer = BuildSerializer();
        Assert.Throws<ArgumentNullException>(() => serializer.Deserialize<SampleRecord>((string)null!));
    }

    [Fact]
    public void Deserialize_InvalidJson_ShouldThrowJsonException()
    {
        var serializer = BuildSerializer();
        Assert.Throws<JsonException>(() => serializer.Deserialize<SampleRecord>("not-valid-json"));
    }

    [Fact]
    public void Deserialize_UnknownProperties_ShouldIgnoreThem()
    {
        var serializer = BuildSerializer();
        var json = "{\"name\":\"ok\",\"value\":1,\"extra\":\"ignored\"}";

        var result = serializer.Deserialize<SampleRecord>(json);

        Assert.NotNull(result);
        Assert.Equal("ok", result!.Name);
    }

    // ---------------------------------------------------------------------------
    // Deserialize<T>(ReadOnlySpan<byte>)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deserialize_Bytes_ShouldReturnObject()
    {
        var serializer = BuildSerializer();
        var bytes = Encoding.UTF8.GetBytes("{\"name\":\"bytes\",\"value\":3}");

        var result = serializer.Deserialize<SampleRecord>(bytes.AsSpan());

        Assert.NotNull(result);
        Assert.Equal("bytes", result!.Name);
        Assert.Equal(3, result.Value);
    }

    [Fact]
    public void Deserialize_Bytes_InvalidJson_ShouldThrowJsonException()
    {
        var serializer = BuildSerializer();
        var bytes = Encoding.UTF8.GetBytes("{ invalid }");

        Assert.Throws<JsonException>(() => serializer.Deserialize<SampleRecord>(bytes.AsSpan()));
    }

    // ---------------------------------------------------------------------------
    // SerializeAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SerializeAsync_ShouldWriteToStream()
    {
        var serializer = BuildSerializer();
        using var stream = new MemoryStream();
        var obj = new SampleRecord("async", 5);

        await serializer.SerializeAsync(stream, obj);

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("async", json, StringComparison.Ordinal);
        Assert.Contains("5", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SerializeAsync_NullStream_ShouldThrowArgumentNullException()
    {
        var serializer = BuildSerializer();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => serializer.SerializeAsync<SampleRecord>(null!, new SampleRecord("x", 1)));
    }

    [Fact]
    public async Task SerializeAsync_NullValue_ShouldThrowArgumentNullException()
    {
        var serializer = BuildSerializer();
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => serializer.SerializeAsync<SampleRecord>(stream, null!));
    }

    [Fact]
    public async Task SerializeAsync_ReadOnlyStream_ShouldThrowArgumentException()
    {
        var serializer = BuildSerializer();
        // Read-only stream (CanWrite = false)
        using var stream = new MemoryStream(new byte[10], writable: false);

        await Assert.ThrowsAsync<ArgumentException>(
            () => serializer.SerializeAsync(stream, new SampleRecord("x", 1)));
    }

    // ---------------------------------------------------------------------------
    // DeserializeAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeserializeAsync_ShouldReturnObject_FromStream()
    {
        var serializer = BuildSerializer();
        var json = "{\"name\":\"fromStream\",\"value\":11}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await serializer.DeserializeAsync<SampleRecord>(stream);

        Assert.NotNull(result);
        Assert.Equal("fromStream", result!.Name);
        Assert.Equal(11, result.Value);
    }

    [Fact]
    public async Task DeserializeAsync_NullStream_ShouldThrowArgumentNullException()
    {
        var serializer = BuildSerializer();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => serializer.DeserializeAsync<SampleRecord>(null!));
    }

    [Fact]
    public async Task DeserializeAsync_WriteOnlyStream_ShouldThrowArgumentException()
    {
        var serializer = BuildSerializer();
        // Write-only stream (CanRead = false)
        using var stream = new WriteOnlyStream();

        await Assert.ThrowsAsync<ArgumentException>(
            () => serializer.DeserializeAsync<SampleRecord>(stream));
    }

    // ---------------------------------------------------------------------------
    // Round-trip
    // ---------------------------------------------------------------------------

    [Fact]
    public void RoundTrip_Serialize_ThenDeserialize_ShouldProduceSameObject()
    {
        var serializer = BuildSerializer();
        var original = new SampleRecord("roundtrip", 123);

        var json = serializer.Serialize(original);
        var restored = serializer.Deserialize<SampleRecord>(json);

        Assert.Equal(original, restored);
    }

    // ---------------------------------------------------------------------------
    // WriteOnlyStream helper
    // ---------------------------------------------------------------------------

    private sealed class WriteOnlyStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) { }
    }
}
