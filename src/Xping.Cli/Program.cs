/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Commands;

namespace Xping.Cli;

/// <summary>
/// Entry point for the <c>xping</c> command-line tool.
/// </summary>
internal static class Program
{
    internal static int Main(string[] args)
    {
        return Run(args, Console.Out, Console.Error, Console.In);
    }

    /// <summary>
    /// Runs the tool against the supplied writers.
    /// </summary>
    /// <remarks>Separated from <see cref="Main"/> so the command surface is testable.</remarks>
    internal static int Run(
        string[] args, TextWriter output, TextWriter error, TextReader? input = null)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteUsage(output);
            return args.Length == 0 ? 1 : 0;
        }

        string verb = args[0];
        string[] rest = args.Skip(1).ToArray();

        switch (verb)
        {
            case "report":
                if (!ReportOptions.TryParse(rest, out ReportOptions options, out string? parseError))
                {
                    error.WriteLine(parseError);
                    error.WriteLine("Run `xping report --help` for usage.");
                    return 2;
                }

                return ReportCommand.Run(options, output);

            case "where":
                return WhereCommand.Run(ValueOf(rest, "--directory"), output);

            case "clear":
                return ClearCommand.Run(
                    ValueOf(rest, "--directory"),
                    ValueOf(rest, "--assembly"),
                    force: rest.Contains("--force"),
                    input ?? TextReader.Null,
                    output,
                    error);

            case "--version":
            case "version":
                output.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");
                return 0;

            default:
                error.WriteLine($"Unknown command '{verb}'.");
                WriteUsage(error);
                return 2;
        }
    }

    private static bool IsHelp(string arg) =>
        arg is "--help" or "-h" or "help";

    /// <summary>Returns the value following a flag, or <see langword="null"/> when absent.</summary>
    private static string? ValueOf(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("xping - local test reliability reports");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  xping report [options]     Report flakiness from recent local runs");
        writer.WriteLine("  xping where [options]      Show where local runs are stored");
        writer.WriteLine("  xping clear [options]      Delete recorded runs");
        writer.WriteLine("  xping version              Print the tool version");
        writer.WriteLine();
        writer.WriteLine("Report options:");
        writer.WriteLine("  --last <n>          Recent runs to analyse per assembly (default 12)");
        writer.WriteLine("  --assembly <name>   Restrict to one test assembly");
        writer.WriteLine("  --all               Report across every assembly in the store");
        writer.WriteLine("  --directory <path>  Resolve the store from this directory");
        writer.WriteLine("  --details           Print per-test run history");
        writer.WriteLine("  --json              Emit JSON instead of a rendered report");
        writer.WriteLine("  --ascii             Force ASCII output");
        writer.WriteLine();
        writer.WriteLine("Clear options:");
        writer.WriteLine("  --assembly <name>   Only delete runs for one test assembly");
        writer.WriteLine("  --force             Skip the confirmation prompt");
        writer.WriteLine();
        writer.WriteLine("Runs are recorded by the Xping SDK. No account is required.");
    }
}
