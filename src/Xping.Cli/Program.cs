/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xping.Cli.Commands;
using Xping.Cli.Hosting;
using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Sdk.Shared;

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
    /// <param name="args">The command line.</param>
    /// <param name="output">Where the report is written.</param>
    /// <param name="error">Where warnings and failures are written.</param>
    /// <param name="input">Where prompts are read from.</param>
    /// <param name="isTerminal">
    /// Whether <paramref name="output"/> is a terminal. Defaults to what the process can see, and is
    /// passed explicitly by tests that exercise the parts of the output only a terminal gets.
    /// </param>
    internal static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        TextReader? input = null,
        bool? isTerminal = null)
    {
        using IHost host = BuildHost(
            output, error, input ?? TextReader.Null, isTerminal ?? !Console.IsOutputRedirected);
        RootCommand root = BuildRootCommand(host.Services, output);

        bool noArgs = args.Length == 0;

        // Matches the historical bare-word `help` verb; `--help`/`-h` are already recognized by
        // the root command itself.
        string[] effectiveArgs = noArgs
            ? ["--help"]
            : args[0] == "help" ? ["--help"] : args;

        ParseResult parseResult = root.Parse(effectiveArgs);

        if (parseResult.Errors.Count > 0)
        {
            foreach (ParseError parseError in parseResult.Errors)
                error.WriteLine(parseError.Message);

            string verb = effectiveArgs.Length > 0 ? effectiveArgs[0] : string.Empty;
            error.WriteLine(verb is "report" or "where" or "clear" or "version"
                ? $"Run `xping {verb} --help` for usage."
                : "Run `xping --help` for usage.");
            return 2;
        }

        int exitCode = parseResult.Invoke(new InvocationConfiguration { Output = output, Error = error });
        return noArgs ? 1 : exitCode;
    }

    /// <summary>
    /// Builds the composition root for one CLI invocation.
    /// </summary>
    /// <remarks>
    /// Built fresh per <see cref="Run"/> call rather than once per process: <paramref name="output"/>
    /// and <paramref name="error"/> are per-invocation state (tests pass a new pair on every call),
    /// and there are no <see cref="IHostedService"/>s registered, so this is purely a composition
    /// root — built, resolved from, and disposed, never started/run.
    /// </remarks>
    private static IHost BuildHost(
        TextWriter output, TextWriter error, TextReader input, bool isTerminal)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // `xping` is a globally-installed tool invoked from arbitrary directories. The default
        // pipeline would load appsettings.json relative to the current directory (ContentRootPath) -
        // silently picking up a stray file from whatever repo the user happens to be in is exactly
        // the kind of surprise a CLI must not have. Nothing today needs IConfiguration; clear it so
        // any future use is deliberate.
        builder.Configuration.Sources.Clear();

        // The default pipeline adds a Console logging provider that writes straight to
        // System.Console, bypassing the output/error test seam and risking interleaving with
        // --json output on real stdout. Start silent; a future --verbose flag is the place to opt a
        // provider back in.
        builder.Logging.ClearProviders();

        builder.Services.AddXpingCliServices(output, error, input, isTerminal);

        return builder.Build();
    }

    private static RootCommand BuildRootCommand(IServiceProvider services, TextWriter output)
    {
        // RootCommand's displayed name comes from the running executable (ExecutableName), which
        // is "xping" once packed as a tool (ToolCommandName in the csproj); under `dotnet run` in
        // this repo it shows the assembly name instead, which is expected and harmless.
        RootCommand root = new(
            "xping - local test reliability reports\n\n" +
            "Runs are recorded by the Xping SDK. No account is required.");

        root.Subcommands.Add(BuildReportCommand(services));
        root.Subcommands.Add(BuildWhereCommand(services));
        root.Subcommands.Add(BuildClearCommand(services));
        root.Subcommands.Add(BuildVersionCommand(output));

        return root;
    }

    private static Command BuildReportCommand(IServiceProvider services)
    {
        // `--last` is the name this option shipped under. Kept as an alias so existing scripts and
        // muscle memory keep working; `--runs` is what the report itself calls the window.
        Option<int?> runsOption = new("--runs", "--last")
        {
            Description = "Recent runs to analyse (default: 20, or 14 days, whichever is fewer)",
            CustomParser = result => ParsePositive(result, "--runs")
        };

        Option<string?> sinceOption = new("--since")
        {
            Description = "Analyse from a commit SHA or a date (yyyy-MM-dd)"
        };

        Option<int?> topOption = new("--top")
        {
            Description = $"Findings to show (default {LocalAnalysisConstants.DefaultTopFindings})",
            CustomParser = result => ParsePositive(result, "--top")
        };

        Option<bool> allOption = new("--all")
        {
            Description = "Show every finding rather than the top ones"
        };

        Option<FindingKind[]> kindOption = new("--kind")
        {
            Description = "Restrict to one or more finding kinds",
            AllowMultipleArgumentsPerToken = true,

            // Parsed by hand so a typo names the kinds that exist. The built-in enum converter
            // reports the CLR type name instead, which tells a user nothing they can act on.
            CustomParser = result =>
            {
                var kinds = new List<FindingKind>();

                foreach (Token token in result.Tokens)
                {
                    if (Enum.TryParse(token.Value, ignoreCase: true, out FindingKind kind))
                        kinds.Add(kind);
                    else
                        result.AddError(
                            $"Unknown finding kind '{token.Value}'. " +
                            $"Expected one of: {string.Join(", ", Enum.GetNames<FindingKind>())}.");
                }

                return [.. kinds];
            }
        };

        Option<ReportFormat> formatOption = new("--format")
        {
            Description = "Output format: text, json or summary",
            DefaultValueFactory = _ => ReportFormat.Text
        };

        // Superseded by `--format json`, kept so existing scripts keep working.
        Option<bool> jsonOption = new("--json")
        {
            Description = "Alias for --format json"
        };

        // The one-line form is asked for by name far more often than by format, so it gets a flag of
        // its own for the same reason `--json` kept one.
        Option<bool> summaryOption = new("--summary")
        {
            Description = "Alias for --format summary"
        };

        Option<FailOn> failOnOption = new("--fail-on")
        {
            Description = "Exit non-zero when a finding reaches this severity",
            DefaultValueFactory = _ => FailOn.None
        };

        Option<string?> assemblyOption = new("--assembly")
        {
            Description = "Restrict to one test assembly"
        };

        Option<string?> directoryOption = new("--directory")
        {
            Description = "Resolve the store from this directory"
        };

        Option<bool> asciiOption = new("--ascii")
        {
            Description = "Force ASCII output"
        };

        // NO_COLOR is honoured too; the flag exists for the caller who cannot set an environment
        // variable, such as a build step that only takes an argument list.
        Option<bool> noColorOption = new("--no-color")
        {
            Description = "Never emit ANSI colour"
        };

        Command command = new("report", "Report test reliability findings from recent local runs")
        {
            runsOption, sinceOption, topOption, allOption, kindOption, formatOption, jsonOption,
            summaryOption, failOnOption, assemblyOption, directoryOption, asciiOption, noColorOption
        };

        // Presence is tested with GetResult rather than GetValue: an option whose own parser already
        // rejected its value has no value to read, and asking for one throws before the parse errors
        // are ever reported to the user.
        command.Validators.Add(result =>
        {
            if (result.GetResult(runsOption) != null && result.GetResult(sinceOption) != null)
                result.AddError("--runs and --since are mutually exclusive.");

            if (result.GetResult(allOption) != null && result.GetResult(topOption) != null)
                result.AddError("--all and --top are mutually exclusive.");

            // The aliases are conveniences, not overrides. Silently winning over an explicit
            // `--format` would make one of the two flags a lie.
            if (result.GetResult(jsonOption) != null && result.GetResult(summaryOption) != null)
                result.AddError("--json and --summary are mutually exclusive.");

            if (result.GetResult(formatOption) is { Implicit: false } format)
            {
                ReportFormat chosen = format.GetValueOrDefault<ReportFormat>();

                if (result.GetResult(jsonOption) != null && chosen != ReportFormat.Json)
                    result.AddError("--json conflicts with --format.");

                if (result.GetResult(summaryOption) != null && chosen != ReportFormat.Summary)
                    result.AddError("--summary conflicts with --format.");
            }
        });

        command.SetAction(parseResult =>
        {
            bool showAll = parseResult.GetValue(allOption);

            var options = new ReportOptions
            {
                Runs = parseResult.GetValue(runsOption),
                Since = parseResult.GetValue(sinceOption),
                Top = showAll
                    ? null
                    : parseResult.GetValue(topOption) ?? LocalAnalysisConstants.DefaultTopFindings,
                Kinds = parseResult.GetValue(kindOption) ?? [],
                Assembly = parseResult.GetValue(assemblyOption),
                Directory = parseResult.GetValue(directoryOption),
                Format = parseResult.GetValue(jsonOption) ? ReportFormat.Json
                    : parseResult.GetValue(summaryOption) ? ReportFormat.Summary
                    : parseResult.GetValue(formatOption),
                FailOn = ToSeverity(parseResult.GetValue(failOnOption)),
                Ascii = parseResult.GetValue(asciiOption),
                NoColor = parseResult.GetValue(noColorOption)
            };

            return services.GetRequiredService<ReportCommand>().Run(options);
        });

        return command;
    }

    /// <summary>
    /// The severities <c>--fail-on</c> accepts, including the opt-out.
    /// </summary>
    /// <remarks>
    /// A separate enum rather than a nullable <c>Severity</c> because the option needs a name for
    /// "never fail" that a user can type, and because it keeps the parser's error message listing
    /// exactly the words that work.
    /// </remarks>
    private enum FailOn
    {
        /// <summary>Never fail on findings.</summary>
        None,

        /// <summary>Fail on any finding.</summary>
        Low,

        /// <summary>Fail on medium and high findings.</summary>
        Medium,

        /// <summary>Fail on high findings only.</summary>
        High
    }

    private static Severity? ToSeverity(FailOn failOn) => failOn switch
    {
        FailOn.High => Severity.High,
        FailOn.Medium => Severity.Medium,
        FailOn.Low => Severity.Low,
        _ => null
    };

    /// <summary>
    /// Parses an option that must be a positive count.
    /// </summary>
    private static int? ParsePositive(ArgumentResult result, string optionName)
    {
        // Defensive: normal arity validation rejects a missing value before this runs, but never
        // assume that holds across parser versions — indexing beats `.Single()` here because it
        // can't throw if a future arity mismatch calls this with 0 or 2+ tokens.
        string raw = result.Tokens.Count == 1 ? result.Tokens[0].Value : string.Empty;

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ||
            parsed <= 0)
        {
            result.AddError($"{optionName} expects a positive number, got '{raw}'.");
            return null;
        }

        return parsed;
    }

    private static Command BuildWhereCommand(IServiceProvider services)
    {
        Option<string?> directoryOption = new("--directory")
        {
            Description = "Resolve the store from this directory"
        };

        Command command = new("where", "Show where local runs are stored") { directoryOption };

        command.SetAction(parseResult =>
            services.GetRequiredService<WhereCommand>().Run(parseResult.GetValue(directoryOption)));

        return command;
    }

    private static Command BuildClearCommand(IServiceProvider services)
    {
        // A trailing `--assembly` with no value must fail parsing rather than silently reading as
        // "no scope" — the latter would widen a scoped delete into deleting everything, the worst
        // possible failure for a destructive command. The option's default arity (exactly one
        // value) already enforces this.
        Option<string?> assemblyOption = new("--assembly")
        {
            Description = "Only delete runs for one test assembly"
        };

        Option<string?> directoryOption = new("--directory")
        {
            Description = "Resolve the store from this directory"
        };

        Option<bool> forceOption = new("--force")
        {
            Description = "Skip the confirmation prompt"
        };

        Command command = new("clear", "Delete recorded runs")
        {
            assemblyOption, directoryOption, forceOption
        };

        command.SetAction(parseResult => services.GetRequiredService<ClearCommand>().Run(
            parseResult.GetValue(directoryOption),
            parseResult.GetValue(assemblyOption),
            parseResult.GetValue(forceOption)));

        return command;
    }

    private static Command BuildVersionCommand(TextWriter output)
    {
        Command command = new("version", "Print the tool version");

        // Uses the shared XpingVersion (the same clean SemVer reported in the SDK's User-Agent
        // header and TestSession.SdkVersion), so it may omit the local source-revision suffix
        // (e.g. "+abc123") that System.CommandLine's built-in `--version` option can include.
        command.SetAction(_ =>
        {
            output.WriteLine(XpingVersion.Current);
            return 0;
        });

        return command;
    }
}
