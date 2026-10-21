/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Services.LocalStore.Internals;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Tests.LocalStore;

public sealed class SessionFileHeaderTests : IDisposable
{
    private readonly string _directory;

    public SessionFileHeaderTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "xping-header-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string WriteGzip(string json)
    {
        string path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".json.gz");

        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        gzip.Write(bytes, 0, bytes.Length);

        return path;
    }

    [Fact]
    public void ReadAssembliesReturnsTheNamesASerializedSessionOpensWith()
    {
        TestSession session = new TestSessionBuilder()
            .WithAssemblies(["Beta.Tests", "Alpha.Tests"])
            .Build();

        string path = WriteGzip(JsonSerializer.Serialize(session, XpingSerializerOptions.FileOptions));

        Assert.Equal(["Alpha.Tests", "Beta.Tests"], SessionFileHeader.ReadAssemblies(path));
    }

    [Fact]
    public void ReadAssembliesReadsANameListLongerThanTheInitialBuffer()
    {
        string[] names = [.. Enumerable.Range(0, 400).Select(i => $"Company.Product.Feature{i:D4}.Tests")];

        string path = WriteGzip(
            "{\"assemblies\":[" + string.Join(",", names.Select(n => $"\"{n}\"")) + "],\"executions\":[]}");

        Assert.Equal(names, SessionFileHeader.ReadAssemblies(path));
    }

    [Fact]
    public void ReadAssembliesReturnsAnEmptyListAsStored()
    {
        string path = WriteGzip("{\"assemblies\":[],\"executions\":[]}");

        Assert.Empty(SessionFileHeader.ReadAssemblies(path)!);
    }

    [Fact]
    public void ReadAssembliesReturnsNullWhenTheFileDoesNotOpenWithThem()
    {
        string path = WriteGzip("{\"sessionId\":\"x\",\"executions\":[],\"assemblies\":[\"Alpha.Tests\"]}");

        Assert.Null(SessionFileHeader.ReadAssemblies(path));
    }

    [Fact]
    public void ReadAssembliesReturnsNullForATruncatedDocument()
    {
        string path = WriteGzip("{\"assemblies\":[\"Alpha.Te");

        Assert.Null(SessionFileHeader.ReadAssemblies(path));
    }

    [Fact]
    public void ReadAssembliesReturnsNullForACorruptFile()
    {
        string path = Path.Combine(_directory, "corrupt.json.gz");
        File.WriteAllBytes(path, [0x1f, 0x8b, 0x08, 0x00, 0xde, 0xad, 0xbe, 0xef]);

        Assert.Null(SessionFileHeader.ReadAssemblies(path));
    }

    [Fact]
    public void ReadAssembliesReturnsNullForAMissingFile()
    {
        Assert.Null(SessionFileHeader.ReadAssemblies(Path.Combine(_directory, "missing.json.gz")));
    }
}
