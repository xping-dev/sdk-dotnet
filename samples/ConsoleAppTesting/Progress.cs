using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Session;

namespace ConsoleAppTesting;

public sealed class Progress(ILogger<Program> logger) : IProgress<TestStep>
{
    private static readonly Action<ILogger, string, Exception?> LogSuccessMessage = LoggerMessage.Define<string>(
        logLevel: LogLevel.Information, eventId: 1, formatString: "{Value}");
    private static readonly Action<ILogger, string, Exception?> LogErrorMessage = LoggerMessage.Define<string>(
        logLevel: LogLevel.Error, eventId: 2, formatString: "{Value}");

    private readonly ILogger<Program> _logger = logger;

    public void Report(TestStep value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        switch (value.Result)
        {
            case TestStepResult.Succeeded:
                LogSuccessMessage(_logger, value.ToString(), null);
                break;
            case TestStepResult.Failed:
                LogErrorMessage(_logger, value.ToString(), null);
                break;
        }
    }
}
