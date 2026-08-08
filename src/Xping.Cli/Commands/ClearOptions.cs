/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Commands;

/// <summary>
/// Options for <c>xping clear</c>.
/// </summary>
/// <remarks>
/// Parsed strictly rather than by scanning for flags. A lenient scan cannot distinguish
/// <c>--assembly Foo</c> from a trailing <c>--assembly</c> with no value, and the second silently
/// widens a scoped delete into "delete everything" — the worst possible failure for a destructive
/// command.
/// </remarks>
internal sealed class ClearOptions
{
    /// <summary>Gets the test assembly to restrict deletion to, or <see langword="null"/> for all.</summary>
    public string? Assembly { get; private set; }

    /// <summary>Gets the directory to resolve the store from.</summary>
    public string? Directory { get; private set; }

    /// <summary>Gets a value indicating whether to skip the confirmation prompt.</summary>
    public bool Force { get; private set; }

    /// <summary>
    /// Parses command-line arguments.
    /// </summary>
    /// <param name="args">Arguments following the verb.</param>
    /// <param name="options">The parsed options.</param>
    /// <param name="error">A message describing why parsing failed.</param>
    /// <returns><see langword="true"/> when parsing succeeded.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out ClearOptions options, out string? error)
    {
        options = new ClearOptions();
        error = null;

        for (int i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--force":
                    options.Force = true;
                    break;

                case "--assembly":
                    if (!TryTakeValue(args, ref i, "--assembly", out string? assembly, out error))
                        return false;
                    options.Assembly = assembly;
                    break;

                case "--directory":
                    if (!TryTakeValue(args, ref i, "--directory", out string? directory, out error))
                        return false;
                    options.Directory = directory;
                    break;

                default:
                    error = $"Unknown option '{args[i]}'.";
                    return false;
            }
        }

        return true;
    }

    private static bool TryTakeValue(
        IReadOnlyList<string> args, ref int index, string name, out string? value, out string? error)
    {
        if (index + 1 >= args.Count)
        {
            value = null;
            error = $"{name} expects a value.";
            return false;
        }

        value = args[++index];
        error = null;
        return true;
    }
}
