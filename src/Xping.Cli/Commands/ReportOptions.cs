/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;

namespace Xping.Cli.Commands;

/// <summary>
/// Options for <c>xping report</c>.
/// </summary>
internal sealed class ReportOptions
{
    /// <summary>Gets the number of recent runs to analyse.</summary>
    public int Last { get; private set; } = 12;

    /// <summary>Gets the test assembly to restrict the report to, or <see langword="null"/> for all.</summary>
    public string? Assembly { get; private set; }

    /// <summary>Gets the directory to resolve the store from. Defaults to the working directory.</summary>
    public string? Directory { get; private set; }

    /// <summary>Gets a value indicating whether per-test history should be printed.</summary>
    public bool Details { get; private set; }

    /// <summary>Gets a value indicating whether to force the ASCII glyph set.</summary>
    public bool Ascii { get; private set; }

    /// <summary>
    /// Gets a value indicating whether to report across every assembly in the store rather than
    /// scoping to one.
    /// </summary>
    public bool All { get; private set; }

    /// <summary>Gets a value indicating whether to emit JSON instead of a rendered block.</summary>
    public bool Json { get; private set; }

    /// <summary>
    /// Parses command-line arguments.
    /// </summary>
    /// <param name="args">Arguments following the verb.</param>
    /// <param name="options">The parsed options.</param>
    /// <param name="error">A message describing why parsing failed.</param>
    /// <returns><see langword="true"/> when parsing succeeded.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out ReportOptions options, out string? error)
    {
        options = new ReportOptions();
        error = null;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];

            switch (arg)
            {
                case "--details":
                    options.Details = true;
                    break;

                case "--ascii":
                    options.Ascii = true;
                    break;

                case "--all":
                    options.All = true;
                    break;

                case "--json":
                    options.Json = true;
                    break;

                case "--last":
                    if (!TryTakeValue(args, ref i, "--last", out string? last, out error))
                        return false;
                    if (!int.TryParse(last, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ||
                        parsed <= 0)
                    {
                        error = $"--last expects a positive number, got '{last}'.";
                        return false;
                    }

                    options.Last = parsed;
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
                    error = $"Unknown option '{arg}'.";
                    return false;
            }
        }

        if (options.All && options.Assembly != null)
        {
            error = "--all and --assembly are mutually exclusive.";
            return false;
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
