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
    internal static int Run(
        string[] args, TextWriter output, TextWriter error, TextReader? input = null)
    {
        using IHost host = BuildHost(output, error, input ?? TextReader.Null);

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
    private static IHost BuildHost(TextWriter output, TextWriter error, TextReader input)
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

        builder.Services.AddXpingCliServices(output, error, input);

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
        Option<int> lastOption = new("--last")
        {
            Description = "Recent runs to analyse per assembly (default 12)",
            DefaultValueFactory = _ => 12,
            CustomParser = result =>
            {
                // Defensive: normal arity validation rejects a missing value before this runs, but
                // never assume that holds across parser versions — indexing beats `.Single()` here
                // because it can't throw if a future arity mismatch calls this with 0 or 2+ tokens.
                string raw = result.Tokens.Count == 1 ? result.Tokens[0].Value : string.Empty;
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ||
                    parsed <= 0)
                {
                    result.AddError($"--last expects a positive number, got '{raw}'.");
                    return 0;
                }

                return parsed;
            }
        };

        Option<string?> assemblyOption = new("--assembly")
        {
            Description = "Restrict to one test assembly"
        };

        Option<bool> allOption = new("--all")
        {
            Description = "Report across every assembly in the store"
        };

        Option<string?> directoryOption = new("--directory")
        {
            Description = "Resolve the store from this directory"
        };

        Option<bool> detailsOption = new("--details")
        {
            Description = "Print per-test run history"
        };

        Option<bool> jsonOption = new("--json")
        {
            Description = "Emit JSON instead of a rendered report"
        };

        Option<bool> asciiOption = new("--ascii")
        {
            Description = "Force ASCII output"
        };

        Command command = new("report", "Report flakiness from recent local runs")
        {
            lastOption, assemblyOption, allOption, directoryOption, detailsOption, jsonOption, asciiOption
        };

        command.Validators.Add(result =>
        {
            if (result.GetValue(allOption) && result.GetValue(assemblyOption) != null)
                result.AddError("--all and --assembly are mutually exclusive.");
        });

        command.SetAction(parseResult =>
        {
            var options = new ReportOptions
            {
                Last = parseResult.GetValue(lastOption),
                Assembly = parseResult.GetValue(assemblyOption),
                Directory = parseResult.GetValue(directoryOption),
                Details = parseResult.GetValue(detailsOption),
                Json = parseResult.GetValue(jsonOption),
                Ascii = parseResult.GetValue(asciiOption),
                All = parseResult.GetValue(allOption)
            };

            return services.GetRequiredService<ReportCommand>().Run(options);
        });

        return command;
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
