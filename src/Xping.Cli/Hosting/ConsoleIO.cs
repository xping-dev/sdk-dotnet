/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Hosting;

/// <summary>
/// The I/O streams for one CLI invocation, threaded through DI so commands can be
/// constructor-injected instead of capturing writers in closures.
/// </summary>
internal sealed class ConsoleIO(TextWriter output, TextWriter error, TextReader input)
{
    public TextWriter Output { get; } = output;
    public TextWriter Error { get; } = error;
    public TextReader Input { get; } = input;
}
