/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Upload;

#pragma warning disable CA2007 // Do not directly await a Task

namespace Xping.Sdk.Core.Tests.Collection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xunit;

public sealed class TestExecutionCollectorTests
{
    private static TestExecution CreateTestExecution(string testName = "Test1")
    {
        return new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestName = testName,
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(100),
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow,
            SessionContext = new TestSession
            {
                SessionId = Guid.NewGuid().ToString(),
                StartedAt = DateTime.UtcNow,
                EnvironmentInfo = new EnvironmentInfo()
            },
            Metadata = new TestMetadata(),
        };
    }

    [Fact]
    public void Constructor_WithNullUploader_ThrowsArgumentNullException()
    {
        var config = new XpingConfiguration { ApiKey = "test", ProjectId = "test" };
        Assert.Throws<ArgumentNullException>(() => new TestExecutionCollector(null!, config));
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        var uploader = new MockXpingUploader();
        Assert.Throws<ArgumentNullException>(() => new TestExecutionCollector(uploader, null!));
    }

    [Fact]
    public async Task RecordTest_WithNullTestExecution_ThrowsArgumentNullException()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration { ApiKey = "test", ProjectId = "test" };
        await using var collector = new TestExecutionCollector(uploader, config);

        Assert.Throws<ArgumentNullException>(() => collector.RecordTest(null!));
    }

    [Fact]
    public async Task RecordTest_WithValidTestExecution_BuffersTest()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration { ApiKey = "test", ProjectId = "test", SamplingRate = 1.0 };
        await using var collector = new TestExecutionCollector(uploader, config);

        collector.RecordTest(CreateTestExecution());

        var stats = await collector.GetStatsAsync();
        Assert.Equal(1, stats.TotalRecorded);
        Assert.Equal(1, stats.TotalSampled);
        Assert.Equal(1, stats.BufferCount);
    }

    [Fact]
    public async Task FlushAsync_WithBufferedTests_UploadsAll()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            BatchSize = 100,
            SamplingRate = 1.0,
        };
        await using var collector = new TestExecutionCollector(uploader, config);

        collector.RecordTest(CreateTestExecution("Test1"));
        collector.RecordTest(CreateTestExecution("Test2"));
        collector.RecordTest(CreateTestExecution("Test3"));
        await collector.FlushAsync();

        Assert.Single(uploader.UploadedBatches);
        Assert.Equal(3, uploader.UploadedBatches[0].Count);
    }

    [Fact]
    public async Task GetStatsAsync_InitialState_ReturnsZeros()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration { ApiKey = "test", ProjectId = "test" };
        await using var collector = new TestExecutionCollector(uploader, config);

        var stats = await collector.GetStatsAsync();

        Assert.Equal(0, stats.TotalRecorded);
        Assert.Equal(0, stats.TotalUploaded);
        Assert.Equal(0, stats.TotalFailed);
        Assert.Equal(0, stats.BufferCount);
    }

    [Fact]
    public async Task GetStatsAsync_AfterRecording_UpdatesCorrectly()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            BatchSize = 100,
            SamplingRate = 1.0,
        };
        await using var collector = new TestExecutionCollector(uploader, config);

        collector.RecordTest(CreateTestExecution("Test1"));
        collector.RecordTest(CreateTestExecution("Test2"));
        var stats = await collector.GetStatsAsync();

        Assert.Equal(2, stats.TotalRecorded);
        Assert.Equal(2, stats.TotalSampled);
        Assert.Equal(2, stats.BufferCount);
        Assert.Equal(0, stats.TotalUploaded);
    }

    [Fact]
    public async Task GetStatsAsync_AfterFlush_UpdatesCorrectly()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            BatchSize = 100,
            SamplingRate = 1.0,
        };
        await using var collector = new TestExecutionCollector(uploader, config);

        collector.RecordTest(CreateTestExecution("Test1"));
        collector.RecordTest(CreateTestExecution("Test2"));
        await collector.FlushAsync();
        var stats = await collector.GetStatsAsync();

        Assert.Equal(2, stats.TotalRecorded);
        Assert.Equal(2, stats.TotalSampled);
        Assert.Equal(2, stats.TotalUploaded);
        Assert.Equal(0, stats.BufferCount);
        Assert.Equal(0, stats.TotalFailed);
    }

    [Fact]
    public async Task SamplingRate_Zero_DropsAllTests()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            BatchSize = 100,
            SamplingRate = 0.0,
        };
        await using var collector = new TestExecutionCollector(uploader, config);

        for (int i = 0; i < 10; i++)
        {
            collector.RecordTest(CreateTestExecution($"Test{i}"));
        }

        await collector.FlushAsync();
        var stats = await collector.GetStatsAsync();

        Assert.Equal(10, stats.TotalRecorded);
        Assert.Equal(0, stats.TotalSampled);
        Assert.Empty(uploader.UploadedBatches);
    }

    [Fact]
    public async Task SamplingRate_One_RecordsAllTests()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            BatchSize = 100,
            SamplingRate = 1.0,
        };
        await using var collector = new TestExecutionCollector(uploader, config);

        for (int i = 0; i < 10; i++)
        {
            collector.RecordTest(CreateTestExecution($"Test{i}"));
        }

        await collector.FlushAsync();
        var stats = await collector.GetStatsAsync();

        Assert.Equal(10, stats.TotalRecorded);
        Assert.Equal(10, stats.TotalSampled);
        Assert.Single(uploader.UploadedBatches);
    }

    [Fact]
    public async Task DisposeAsync_WithBufferedTests_FlushesBeforeDisposing()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            BatchSize = 100,
            SamplingRate = 1.0,
        };
        var collector = new TestExecutionCollector(uploader, config);

        collector.RecordTest(CreateTestExecution("Test1"));
        collector.RecordTest(CreateTestExecution("Test2"));
        await collector.DisposeAsync();

        // Verify upload happened
        Assert.Single(uploader.UploadedBatches);
        Assert.Equal(2, uploader.UploadedBatches[0].Count);
    }

    [Fact]
    public async Task UploadFailure_UpdatesFailedCount()
    {
        var uploader = new MockXpingUploader(shouldFail: true);
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            BatchSize = 100,
            SamplingRate = 1.0,
            MaxRetries = 0,
        };
        await using var collector = new TestExecutionCollector(uploader, config);

        collector.RecordTest(CreateTestExecution("Test1"));
        await collector.FlushAsync();
        var stats = await collector.GetStatsAsync();

        Assert.Equal(1, stats.TotalRecorded);
        Assert.Equal(1, stats.TotalFailed);
    }

    [Fact]
    public async Task RecordTest_ConcurrentCalls_AllTestsRecorded()
    {
        var uploader = new MockXpingUploader();
        var config = new XpingConfiguration
        {
            ApiKey = "test",
            ProjectId = "test",
            BatchSize = 1000,
            SamplingRate = 1.0,
        };
        await using var collector = new TestExecutionCollector(uploader, config);

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            collector.RecordTest(CreateTestExecution($"Test{i}"));
        })).ToArray();

        await Task.WhenAll(tasks);
        await collector.FlushAsync();

        Assert.Single(uploader.UploadedBatches);
        Assert.Equal(100, uploader.UploadedBatches[0].Count);
    }

    private sealed class MockXpingUploader : IXpingUploader
    {
        private readonly bool _shouldFail;
        private readonly TimeSpan _uploadDelay;

        public List<List<TestExecution>> UploadedBatches { get; } = new();
        public List<TestSession> UploadedSessions { get; } = new();
        public TestSession? CurrentSession { get; private set; }

        public MockXpingUploader(bool shouldFail = false, TimeSpan uploadDelay = default)
        {
            _shouldFail = shouldFail;
            _uploadDelay = uploadDelay;
        }

        public void SetSession(TestSession session)
        {
            CurrentSession = session;
        }

        public async Task<UploadResult> UploadSessionAsync(
            TestSession session,
            CancellationToken cancellationToken = default)
        {
            if (_uploadDelay > TimeSpan.Zero)
            {
                await Task.Delay(_uploadDelay, cancellationToken);
            }

            if (_shouldFail)
            {
                return new UploadResult
                {
                    Success = false,
                    ErrorMessage = "Simulated session upload failure",
                };
            }

            UploadedSessions.Add(session);

            return new UploadResult
            {
                Success = true,
                ReceiptId = session.SessionId,
            };
        }

        public async Task<UploadResult> UploadAsync(
            IEnumerable<TestExecution> executions,
            CancellationToken cancellationToken = default)
        {
            if (_uploadDelay > TimeSpan.Zero)
            {
                await Task.Delay(_uploadDelay, cancellationToken);
            }

            if (_shouldFail)
            {
                return new UploadResult
                {
                    Success = false,
                    ErrorMessage = "Simulated upload failure",
                };
            }

            var batch = executions as List<TestExecution> ?? executions.ToList();
            UploadedBatches.Add(batch);

            return new UploadResult
            {
                Success = true,
                ExecutionCount = batch.Count,
            };
        }
    }
}

#pragma warning restore CA2007
#pragma warning restore CA1707
