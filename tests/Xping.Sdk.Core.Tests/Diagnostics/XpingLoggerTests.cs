/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Diagnostics;

using System;
using System.IO;
using Xping.Sdk.Core.Diagnostics;
using Xunit;

public sealed class XpingLoggerTests
{
    [Fact]
    public void XpingNullLogger_Instance_ReturnsSameInstance()
    {
        // Act
        var instance1 = XpingNullLogger.Instance;
        var instance2 = XpingNullLogger.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void XpingNullLogger_IsEnabled_AlwaysReturnsFalse()
    {
        // Arrange
        var logger = XpingNullLogger.Instance;

        // Assert
        Assert.False(logger.IsEnabled(XpingLogLevel.None));
        Assert.False(logger.IsEnabled(XpingLogLevel.Error));
        Assert.False(logger.IsEnabled(XpingLogLevel.Warning));
        Assert.False(logger.IsEnabled(XpingLogLevel.Info));
        Assert.False(logger.IsEnabled(XpingLogLevel.Debug));
    }

    [Fact]
    public void XpingNullLogger_LogError_DoesNotThrow()
    {
        // Arrange
        var logger = XpingNullLogger.Instance;

        // Act & Assert - should not throw
        logger.LogError("test error");
        logger.LogError(null!);
        logger.LogError(string.Empty);
    }

    [Fact]
    public void XpingNullLogger_LogWarning_DoesNotThrow()
    {
        // Arrange
        var logger = XpingNullLogger.Instance;

        // Act & Assert - should not throw
        logger.LogWarning("test warning");
        logger.LogWarning(null!);
        logger.LogWarning(string.Empty);
    }

    [Fact]
    public void XpingNullLogger_LogInfo_DoesNotThrow()
    {
        // Arrange
        var logger = XpingNullLogger.Instance;

        // Act & Assert - should not throw
        logger.LogInfo("test info");
        logger.LogInfo(null!);
        logger.LogInfo(string.Empty);
    }

    [Fact]
    public void XpingNullLogger_LogDebug_DoesNotThrow()
    {
        // Arrange
        var logger = XpingNullLogger.Instance;

        // Act & Assert - should not throw
        logger.LogDebug("test debug");
        logger.LogDebug(null!);
        logger.LogDebug(string.Empty);
    }

    [Fact]
    public void XpingConsoleLogger_DefaultConstructor_SetsInfoLevel()
    {
        // Act
        var logger = new XpingConsoleLogger();

        // Assert
        Assert.False(logger.IsEnabled(XpingLogLevel.None));
        Assert.True(logger.IsEnabled(XpingLogLevel.Error));
        Assert.True(logger.IsEnabled(XpingLogLevel.Warning));
        Assert.True(logger.IsEnabled(XpingLogLevel.Info));
        Assert.False(logger.IsEnabled(XpingLogLevel.Debug));
    }

    [Theory]
    [InlineData(XpingLogLevel.None, false, false, false, false)]
    [InlineData(XpingLogLevel.Error, true, false, false, false)]
    [InlineData(XpingLogLevel.Warning, true, true, false, false)]
    [InlineData(XpingLogLevel.Info, true, true, true, false)]
    [InlineData(XpingLogLevel.Debug, true, true, true, true)]
    public void XpingConsoleLogger_IsEnabled_RespectsMinLevel(
        XpingLogLevel minLevel,
        bool errorEnabled,
        bool warningEnabled,
        bool infoEnabled,
        bool debugEnabled)
    {
        // Arrange
        var logger = new XpingConsoleLogger(minLevel);

        // Assert
        Assert.Equal(errorEnabled, logger.IsEnabled(XpingLogLevel.Error));
        Assert.Equal(warningEnabled, logger.IsEnabled(XpingLogLevel.Warning));
        Assert.Equal(infoEnabled, logger.IsEnabled(XpingLogLevel.Info));
        Assert.Equal(debugEnabled, logger.IsEnabled(XpingLogLevel.Debug));
    }

    [Fact]
    public void XpingConsoleLogger_LogError_WritesToStandardError()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Error);
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        try
        {
            // Act
            logger.LogError("Test error message");

            // Assert
            var output = errorWriter.ToString();
            Assert.Contains("[Xping SDK] ERROR: Test error message", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogWarning_WritesToStandardOutput()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Warning);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            logger.LogWarning("Test warning message");

            // Assert
            var output = outWriter.ToString();
            Assert.Contains("[Xping SDK] WARNING: Test warning message", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogInfo_WritesToStandardOutput()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Info);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            logger.LogInfo("Test info message");

            // Assert
            var output = outWriter.ToString();
            Assert.Contains("[Xping SDK] Test info message", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogDebug_WritesToStandardOutput()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Debug);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            logger.LogDebug("Test debug message");

            // Assert
            var output = outWriter.ToString();
            Assert.Contains("[Xping SDK] DEBUG: Test debug message", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogError_DoesNotWriteWhenDisabled()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.None);
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        try
        {
            // Act
            logger.LogError("This should not appear");

            // Assert
            var output = errorWriter.ToString();
            Assert.Empty(output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogWarning_DoesNotWriteWhenDisabled()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Error);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            logger.LogWarning("This should not appear");

            // Assert
            var output = outWriter.ToString();
            Assert.Empty(output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogInfo_DoesNotWriteWhenDisabled()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Warning);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            logger.LogInfo("This should not appear");

            // Assert
            var output = outWriter.ToString();
            Assert.Empty(output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogDebug_DoesNotWriteWhenDisabled()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Info);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            logger.LogDebug("This should not appear");

            // Assert
            var output = outWriter.ToString();
            Assert.Empty(output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task XpingConsoleLogger_MultipleThreads_WriteSafely()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Info);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act - log from multiple threads
            var tasks = new System.Threading.Tasks.Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                int threadId = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    logger.LogInfo($"Message from thread {threadId}");
                });
            }

            await System.Threading.Tasks.Task.WhenAll(tasks);

            // Assert - should have all messages without corruption
            var output = outWriter.ToString();
            for (int i = 0; i < tasks.Length; i++)
            {
                Assert.Contains($"Message from thread {i}", output, StringComparison.Ordinal);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogWithNullMessage_DoesNotThrow()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Debug);
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errorWriter);

        try
        {
            // Act & Assert - should not throw
            logger.LogError(null!);
            logger.LogWarning(null!);
            logger.LogInfo(null!);
            logger.LogDebug(null!);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogWithEmptyMessage_WritesPrefix()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Info);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            logger.LogInfo(string.Empty);

            // Assert
            var output = outWriter.ToString();
            Assert.Contains("[Xping SDK]", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogWithSpecialCharacters_WritesCorrectly()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Info);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var specialMessage = "Test with special chars: \n\t\"quotes\" and 'apostrophes'";
            logger.LogInfo(specialMessage);

            // Assert
            var output = outWriter.ToString();
            Assert.Contains(specialMessage, output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void XpingConsoleLogger_LogWithLongMessage_WritesCompletely()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Info);
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var longMessage = new string('A', 10000);
            logger.LogInfo(longMessage);

            // Assert
            var output = outWriter.ToString();
            Assert.Contains(longMessage, output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void XpingConsoleLogger_AllLevels_WriteWithCorrectPrefix()
    {
        // Arrange
        var logger = new XpingConsoleLogger(XpingLogLevel.Debug);
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errorWriter);

        try
        {
            // Act
            logger.LogError("error");
            logger.LogWarning("warning");
            logger.LogInfo("info");
            logger.LogDebug("debug");

            // Assert
            var errorOutput = errorWriter.ToString();
            var stdOutput = outWriter.ToString();

            Assert.Contains("[Xping SDK] ERROR: error", errorOutput, StringComparison.Ordinal);
            Assert.Contains("[Xping SDK] WARNING: warning", stdOutput, StringComparison.Ordinal);
            Assert.Contains("[Xping SDK] info", stdOutput, StringComparison.Ordinal);
            Assert.Contains("[Xping SDK] DEBUG: debug", stdOutput, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
