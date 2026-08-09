/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Commands;

/// <summary>
/// Options for <c>xping where</c>.
/// </summary>
internal sealed class WhereOptions
{
    /// <summary>Gets the directory to resolve the store from.</summary>
    public string? Directory { get; private set; }

    /// <summary>
    /// Parses command-line arguments.
    /// </summary>
    /// <param name="args">Arguments following the verb.</param>
    /// <param name="options">The parsed options.</param>
    /// <param name="error">A message describing why parsing failed.</param>
    /// <returns><see langword="true"/> when parsing succeeded.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out WhereOptions options, out string? error)
    {
        options = new WhereOptions();
        error = null;

        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] != "--directory")
            {
                error = $"Unknown option '{args[i]}'.";
                return false;
            }

            if (i + 1 >= args.Count)
            {
                error = "--directory expects a value.";
                return false;
            }

            options.Directory = args[++i];
        }

        return true;
    }
}
