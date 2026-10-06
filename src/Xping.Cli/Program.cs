/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xping.Cli.Auth;
using Xping.Cli.Commands;
using Xping.Cli.Commands.Auth;
using Xping.Cli.Configuration;
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
        using var cancellation = new CancellationTokenSource();
        var cancelKeys = new CancelKeyHandler(cancellation);

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) => e.Cancel = cancelKeys.OnCancelKeyPress();

        Console.CancelKeyPress += OnCancelKeyPress;
        try
        {
            return Run(args, Console.Out, Console.Error, Console.In, cancelKeys: cancelKeys);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
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
    /// Whether stdin, stdout and stderr are terminals. Defaults to what the process can see, stream by
    /// stream, and is passed explicitly by tests that exercise the parts only a terminal gets.
    /// </param>
    /// <param name="configureServices">
    /// Replaces registrations after the real ones are made. Tests use it to substitute the parts
    /// that reach outside the process: the browser, the clock, the credential store.
    /// </param>
    /// <param name="cancelKeys">
    /// Ctrl+C handling. Commands that stop on its token mark themselves cooperative while they run;
    /// <see langword="null"/> means nothing cancels.
    /// </param>
    internal static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        TextReader? input = null,
        bool? isTerminal = null,
        Action<IServiceCollection>? configureServices = null,
        CancelKeyHandler? cancelKeys = null)
    {
        CancellationToken cancellationToken = cancelKeys?.Token ?? CancellationToken.None;

        using IHost host = BuildHost(
            output,
            error,
            input ?? TextReader.Null,
            isTerminal is { } all ? Terminals.All(all) : Terminals.Detect(),
            configureServices);

        var globals = new GlobalOptionSet();
        RootCommand root = BuildRootCommand(host.Services, output, globals, cancelKeys);

        bool noArgs = args.Length == 0;

        // Matches the historical bare-word `help` verb; `--help`/`-h` are already recognized by
        // the root command itself.
        string[] effectiveArgs = noArgs
            ? ["--help"]
            : args[0] == "help" ? ["--help"] : args;

        ParseResult parseResult = root.Parse(effectiveArgs);

        if (parseResult.Errors.Count > 0)
        {
            // Scrubbed although no message should hold a secret: the parser quotes what it could not
            // place, and an argument can be one.
            foreach (ParseError parseError in parseResult.Errors)
                error.WriteLine(Redaction.Scrub(parseError.Message));

            // From the parse rather than the first argument: a global option can come first.
            error.WriteLine(CommandPath(parseResult.CommandResult) is { } path
                ? $"Run `xping {path} --help` for usage."
                : "Run `xping --help` for usage.");
            return 2;
        }

        GlobalOptions options = host.Services.GetRequiredService<GlobalOptions>();
        options.CloudUrl = parseResult.GetValue(globals.CloudUrl);
        options.ApiKey = parseResult.GetValue(globals.ApiKey);
        options.Verbose = parseResult.GetValue(globals.Verbose);

        Redaction.AddSecret(options.ApiKey);

        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error,

            // Main owns Ctrl+C; the parser's own handler would race it for the same signal.
            ProcessTerminationTimeout = null,

            // Escaping exceptions are reported below, scrubbed. The default handler prints the raw
            // message and stack trace, which is where a token in an exception message would leak.
            EnableDefaultExceptionHandler = false
        };

        int exitCode;
        try
        {
            // Blocking is safe here: a console app has no synchronization context to deadlock on,
            // and keeping Run synchronous keeps every existing caller and test as it is.
            exitCode = parseResult.InvokeAsync(configuration, cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            error.WriteLine("Cancelled.");
            return AuthExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            error.WriteLine($"xping: {Redaction.Scrub(ex.Message)}");
            if (options.Verbose)
                error.WriteLine(Redaction.Scrub(ex.ToString()));

            return 1;
        }

        return noArgs ? 1 : exitCode;
    }

    /// <summary>
    /// The words that name a subcommand, such as <c>auth status</c>, or <see langword="null"/> at
    /// the root.
    /// </summary>
    private static string? CommandPath(CommandResult result)
    {
        var names = new List<string>();

        for (SymbolResult? current = result; current is CommandResult { Command: not RootCommand } command; current = command.Parent)
            names.Insert(0, command.Command.Name);

        return names.Count == 0 ? null : string.Join(' ', names);
    }

    /// <summary>
    /// The options every command accepts.
    /// </summary>
    /// <remarks>
    /// Recursive on the root, so <c>xping report --cloud-url …</c> and <c>xping --cloud-url … report</c>
    /// both work, as they do for <c>--help</c>.
    /// </remarks>
    private sealed class GlobalOptionSet
    {
        public Option<string?> CloudUrl { get; } = new("--cloud-url")
        {
            Description = $"Xping Cloud URL (default: {Configuration.CloudUrl.Default})",
            Recursive = true,
            CustomParser = result =>
            {
                string raw = result.Tokens.Count == 1 ? result.Tokens[0].Value : string.Empty;

                if (Configuration.CloudUrl.TryNormalize(raw, "--cloud-url", out string? normalized, out string? error))
                    return normalized;

                result.AddError(error);
                return null;
            }
        };

        public Option<string?> ApiKey { get; } = new("--api-key")
        {
            Description =
                "API key for reading Xping Cloud data; prefer XPING_APIKEY so the key does not land " +
                "in shell history",
            Recursive = true
        };

        public Option<bool> Verbose { get; } = new("--verbose")
        {
            Description = "Write diagnostics to stderr",
            Recursive = true
        };
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
        TextWriter output,
        TextWriter error,
        TextReader input,
        Terminals terminals,
        Action<IServiceCollection>? configureServices)
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
        // --json output on real stdout. The one provider added back is silent unless --verbose was
        // given, and writes through the same error writer as everything else.
        builder.Logging.ClearProviders();
        builder.Logging.Services.AddSingleton<ILoggerProvider, VerboseLoggerProvider>();

        builder.Services.AddXpingCliServices(output, error, input, terminals);
        configureServices?.Invoke(builder.Services);

        return builder.Build();
    }

    private static RootCommand BuildRootCommand(
        IServiceProvider services, TextWriter output, GlobalOptionSet globals, CancelKeyHandler? cancelKeys)
    {
        // The name in the usage line comes from the entry assembly, which the csproj pins to
        // "xping" via AssemblyName. Under `dotnet test` here it is the test host's name instead,
        // so only the packed tool shows the real thing - the release workflow asserts on it.
        RootCommand root = new(
            "xping - local test reliability reports\n\n" +
            "Runs are recorded by the Xping SDK. No account is required.");

        root.Subcommands.Add(BuildReportCommand(services));
        root.Subcommands.Add(BuildWhereCommand(services));
        root.Subcommands.Add(BuildClearCommand(services));
        root.Subcommands.Add(BuildLoginCommand(services, cancelKeys));
        root.Subcommands.Add(BuildLogoutCommand(services, cancelKeys));
        root.Subcommands.Add(BuildAuthCommand(services, cancelKeys));

        root.Options.Add(globals.CloudUrl);
        root.Options.Add(globals.ApiKey);
        root.Options.Add(globals.Verbose);

        UseCleanVersion(root, output);

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
            // Rows, not the N most severe: a second finding about a test already listed sits under
            // the first whatever its severity, and the cut counts it there.
            Description =
                $"Rows to show (default {LocalAnalysisConstants.DefaultTopFindings}); " +
                "findings about one test stay together, so a lower one can be shown ahead of " +
                "a higher one about another test",
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

        Option<string?> idOption = new("--id")
        {
            Description = "Show one finding in detail, by the id on its row",
            CustomParser = ParseFindingId
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
            summaryOption, failOnOption, assemblyOption, idOption, directoryOption, asciiOption,
            noColorOption
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

            // `--id` is its own selection, and every flag here would either change which findings
            // exist or say something about more than one of them.
            if (result.GetResult(idOption) != null)
            {
                if (result.GetResult(kindOption) != null)
                    result.AddError("--id and --kind are mutually exclusive.");

                if (result.GetResult(topOption) != null)
                    result.AddError("--id and --top are mutually exclusive.");

                if (result.GetResult(allOption) != null)
                    result.AddError("--id and --all are mutually exclusive.");

                if (result.GetResult(failOnOption) is { Implicit: false })
                    result.AddError("--id and --fail-on are mutually exclusive.");

                if (result.GetResult(summaryOption) != null)
                    result.AddError("--id and --summary are mutually exclusive.");

                if (result.GetResult(formatOption) is { Implicit: false } idFormat
                    && idFormat.GetValueOrDefault<ReportFormat>() == ReportFormat.Summary)
                {
                    result.AddError("--id and --format summary are mutually exclusive.");
                }
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
                Id = parseResult.GetValue(idOption),
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
    /// Parses a finding id: <c>f_</c> and eight hex digits, the whole of it in either case.
    /// </summary>
    /// <remarks>
    /// Rejected here rather than reported as absent: a mistyped id and one that has moved are
    /// different mistakes with different fixes, and one message for both teaches the reader that a
    /// healed finding and a typo look alike.
    /// </remarks>
    private static string? ParseFindingId(ArgumentResult result)
    {
        string raw = result.Tokens.Count == 1 ? result.Tokens[0].Value : string.Empty;

        if (raw.Length != 10 || !raw.StartsWith("f_", StringComparison.OrdinalIgnoreCase) || !raw[2..].All(char.IsAsciiHexDigit))
        {
            result.AddError($"--id expects a finding id of the form f_ followed by 8 hex digits, got '{raw}'.");
            return null;
        }

        return raw;
    }

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

    /// <summary>
    /// Parses a workspace id: a ULID, 26 Crockford base32 characters in either case.
    /// </summary>
    /// <remarks>
    /// Checked here so a typo fails before the browser opens, not as a consent page that silently
    /// ignores the preselection (cli-auth-cli-spec §3.2).
    /// </remarks>
    private static string? ParseWorkspaceId(ArgumentResult result)
    {
        const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        string raw = result.Tokens.Count == 1 ? result.Tokens[0].Value : string.Empty;

        // The first character carries only the top 3 of 128 bits, so it is at most 7.
        if (raw.Length != 26 || raw[0] > '7' || !raw.All(c => Crockford.Contains(char.ToUpperInvariant(c), StringComparison.Ordinal)))
        {
            result.AddError($"--workspace expects a workspace id (26 characters, as `xping auth status` shows it), got '{raw}'.");
            return null;
        }

        return raw;
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

    private static Command BuildLoginCommand(IServiceProvider services, CancelKeyHandler? cancelKeys)
    {
        Option<bool> jsonOption = new("--json")
        {
            Description = "Write the result to stdout as JSON"
        };

        Option<bool> deviceOption = new("--device")
        {
            Description = "Sign in with a code on any device, for a browser on another machine"
        };

        Option<bool> noBrowserOption = new("--no-browser")
        {
            Description = "Print the sign-in link without trying to open a browser"
        };

        Option<string?> workspaceOption = new("--workspace")
        {
            Description = "Preselect this workspace id on the consent page",
            CustomParser = ParseWorkspaceId
        };

        Command command = new("login", "Sign in to Xping Cloud from your browser")
        {
            deviceOption, noBrowserOption, workspaceOption, jsonOption
        };

        command.SetAction((parseResult, cancellationToken) => RunCooperatively(
            cancelKeys,
            () => services.GetRequiredService<LoginCommand>().RunAsync(
                new LoginOptions(
                    parseResult.GetValue(jsonOption),
                    parseResult.GetValue(deviceOption),
                    parseResult.GetValue(noBrowserOption),
                    parseResult.GetValue(workspaceOption)),
                cancellationToken)));

        return command;
    }

    private static Command BuildLogoutCommand(IServiceProvider services, CancelKeyHandler? cancelKeys)
    {
        Option<bool> jsonOption = new("--json")
        {
            Description = "Write the result to stdout as JSON"
        };

        Command command = new("logout", "Sign out of Xping Cloud and remove the stored sign-in") { jsonOption };

        command.SetAction((parseResult, cancellationToken) => RunCooperatively(
            cancelKeys,
            () => services.GetRequiredService<LogoutCommand>().RunAsync(parseResult.GetValue(jsonOption), cancellationToken)));

        return command;
    }

    private static Command BuildAuthCommand(IServiceProvider services, CancelKeyHandler? cancelKeys)
    {
        Option<bool> jsonOption = new("--json")
        {
            Description = "Write the result to stdout as JSON"
        };

        Command status = new("status", "Show which Xping Cloud credential is in use, without a network call")
        {
            jsonOption
        };

        status.SetAction((parseResult, cancellationToken) => RunCooperatively(
            cancelKeys,
            () => services.GetRequiredService<AuthStatusCommand>().RunAsync(parseResult.GetValue(jsonOption), cancellationToken)));

        return new Command("auth", "Inspect the Xping Cloud sign-in") { status };
    }

    /// <summary>
    /// Runs a command that stops on its cancellation token, so Ctrl+C cancels it instead of ending
    /// the process.
    /// </summary>
    private static async Task<int> RunCooperatively(CancelKeyHandler? cancelKeys, Func<Task<int>> run)
    {
        using IDisposable? cooperative = cancelKeys?.EnterCooperative();
        return await run().ConfigureAwait(false);
    }

    /// <summary>
    /// Points the built-in <c>--version</c> option at the shared <see cref="XpingVersion"/>.
    /// </summary>
    /// <remarks>
    /// The built-in action prints the raw informational version, which on a local build carries the
    /// source-revision suffix (e.g. <c>1.0.0-rc.5+c670fc3…</c>). <see cref="XpingVersion.Current"/>
    /// is the same clean SemVer the SDK reports in its User-Agent header and
    /// <c>TestSession.SdkVersion</c>, so the two never disagree about what version is installed.
    /// The option itself is left in place so its "cannot be combined with other arguments"
    /// validation still applies.
    /// </remarks>
    private static void UseCleanVersion(RootCommand root, TextWriter output)
    {
        foreach (Option option in root.Options)
        {
            if (option is VersionOption versionOption)
                versionOption.Action = new PrintVersionAction(output);
        }
    }

    private sealed class PrintVersionAction(TextWriter output) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            output.WriteLine(XpingVersion.Current);
            return 0;
        }
    }
}
