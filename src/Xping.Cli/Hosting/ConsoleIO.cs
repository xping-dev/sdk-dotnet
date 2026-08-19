/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Hosting;

/// <summary>
/// The I/O streams for one CLI invocation, threaded through DI so commands can be
/// constructor-injected instead of capturing writers in closures.
/// </summary>
internal sealed class ConsoleIO(
    TextWriter output, TextWriter error, TextReader input, bool isTerminal)
{
    public TextWriter Output { get; } = output;
    public TextWriter Error { get; } = error;
    public TextReader Input { get; } = input;

    /// <summary>
    /// Gets a value indicating whether <see cref="Output"/> is a terminal a person is watching.
    /// </summary>
    /// <remarks>
    /// Carried here rather than read from <see cref="Console"/> where it is needed. The writers are
    /// already the seam a test substitutes; asking the process about a stream it was not given is
    /// how a behaviour ends up impossible to exercise from the outside.
    /// </remarks>
    public bool IsTerminal { get; } = isTerminal;
}
