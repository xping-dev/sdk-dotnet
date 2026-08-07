/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;

namespace Xping.Sdk.Core.Services.Reporting.Internals;

/// <summary>
/// Writes the report to the underlying OS stdout handle.
/// </summary>
/// <remarks>
/// MSTest, NUnit3TestAdapter and vstest all call <see cref="Console.SetOut"/> to capture per-test
/// output. Writing through <see cref="Console.Out"/> would land the report in whichever test's
/// capture buffer happened to be active, where the developer will never see it.
/// <see cref="Console.OpenStandardOutput()"/> returns the real handle and bypasses that redirect,
/// which is the same approach <c>RawConsoleSink</c> uses for log output.
/// </remarks>
internal static class RawConsoleReportSink
{
    /// <summary>
    /// Writes text to the raw stdout handle, swallowing any I/O failure.
    /// </summary>
    /// <param name="text">The text to write.</param>
    public static void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            // The stream is opened per call rather than cached: the report is written once, during
            // shutdown, and holding the raw handle open beyond that can interfere with host teardown.
            using Stream stream = Console.OpenStandardOutput();
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };

            writer.Write(text);
        }
        catch (IOException)
        {
            // A closed or redirected handle must never disturb the test run.
        }
        catch (ObjectDisposedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }
}
