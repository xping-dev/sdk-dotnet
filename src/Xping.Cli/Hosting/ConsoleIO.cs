/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Hosting;

/// <summary>
/// Which of the three standard streams are attached to a terminal.
/// </summary>
/// <param name="Input">Whether stdin is a terminal.</param>
/// <param name="Output">Whether stdout is a terminal.</param>
/// <param name="Error">Whether stderr is a terminal.</param>
internal readonly record struct Terminals(bool Input, bool Output, bool Error)
{
    /// <summary>
    /// Every stream a terminal, or none: what a test means by "run it in a terminal".
    /// </summary>
    public static Terminals All(bool isTerminal) => new(isTerminal, isTerminal, isTerminal);

    /// <summary>
    /// What the process can see.
    /// </summary>
    public static Terminals Detect() =>
        new(!Console.IsInputRedirected, !Console.IsOutputRedirected, !Console.IsErrorRedirected);
}

/// <summary>
/// The I/O streams for one CLI invocation, threaded through DI so commands can be
/// constructor-injected instead of capturing writers in closures.
/// </summary>
internal sealed class ConsoleIO(
    TextWriter output, TextWriter error, TextReader input, Terminals terminals)
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
    public bool IsTerminal { get; } = terminals.Output;

    /// <summary>
    /// Gets a value indicating whether <see cref="Input"/> is a terminal a person can type into.
    /// </summary>
    public bool IsInputTerminal { get; } = terminals.Input;

    /// <summary>
    /// Gets a value indicating whether <see cref="Error"/> is a terminal a person is watching.
    /// </summary>
    /// <remarks>
    /// The auth commands write all their text to stderr, so this, not <see cref="IsTerminal"/>,
    /// decides their decoration (cli-auth-cli-spec §3.1).
    /// </remarks>
    public bool IsErrorTerminal { get; } = terminals.Error;
}
