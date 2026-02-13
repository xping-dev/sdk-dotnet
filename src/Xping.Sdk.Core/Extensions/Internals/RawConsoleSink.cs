/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace Xping.Sdk.Core.Extensions.Internals;

/// <summary>
/// A Serilog sink that writes directly to the underlying OS console streams,
/// bypassing any <see cref="Console.SetOut"/> or <see cref="Console.SetError"/> redirections
/// applied by test framework adapters (MSTest, NUnit3TestAdapter) or vstest infrastructure.
/// </summary>
/// <remarks>
/// Test frameworks capture per-test output by calling <c>Console.SetOut()</c> and
/// <c>Console.SetError()</c>. The standard Serilog Console sink writes to those redirected
/// properties, so its output ends up in the test framework's capture buffer and may not
/// appear in the terminal at the verbosity levels used by default CI runs.
///
/// <see cref="Console.OpenStandardOutput()"/> and <see cref="Console.OpenStandardError()"/>
/// return the underlying OS stream handles, which bypass the .NET-level redirect and
/// write directly to the pipe that the test host exposes to the vstest runner.
/// Warning-level and above go to stderr; everything below goes to stdout.
/// </remarks>
internal sealed class RawConsoleSink : ILogEventSink
{
    // Open the raw OS handles at once. Console.OpenStandard* bypasses Console.SetOut/SetError,
    // so these writers are unaffected by any later redirections made by test frameworks.
    private static readonly TextWriter _stdout =
        TextWriter.Synchronized(
            new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true });

    private static readonly TextWriter _stderr =
        TextWriter.Synchronized(
            new StreamWriter(Console.OpenStandardError(), Encoding.UTF8) { AutoFlush = true });

    private readonly ITextFormatter _formatter;

    internal RawConsoleSink(ITextFormatter formatter)
    {
        _formatter = formatter;
    }

    public void Emit(LogEvent logEvent)
    {
        var writer = logEvent.Level >= LogEventLevel.Warning ? _stderr : _stdout;
        _formatter.Format(logEvent, writer);
    }
}
