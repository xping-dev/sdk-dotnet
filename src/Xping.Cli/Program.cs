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
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>
    /// Runs the tool against the supplied writers.
    /// </summary>
    /// <remarks>Separated from <see cref="Main"/> so the command surface is testable.</remarks>
    internal static int Run(string[] args, TextWriter output, TextWriter error)
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

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("xping - local test reliability reports");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  xping report [options]     Report flakiness from recent local runs");
        writer.WriteLine("  xping version              Print the tool version");
        writer.WriteLine();
        writer.WriteLine("Report options:");
        writer.WriteLine("  --last <n>          Number of recent runs to analyse (default 12)");
        writer.WriteLine("  --assembly <name>   Restrict to one test assembly");
        writer.WriteLine("  --directory <path>  Resolve the store from this directory");
        writer.WriteLine("  --details           Print per-test run history");
        writer.WriteLine("  --ascii             Force ASCII output");
        writer.WriteLine();
        writer.WriteLine("Runs are recorded by the Xping SDK. No account is required.");
    }
}
