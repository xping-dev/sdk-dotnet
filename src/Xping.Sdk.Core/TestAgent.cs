/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Session.Collector;
using System.Security.Cryptography;

namespace Xping.Sdk.Core;

/// <summary>
/// The TestAgent class is the main class that performs the testing logic of the Xping SDK. It runs test components, 
/// for example, action or validation steps, such as DnsLookup, IPAddressAccessibilityCheck, etc., using the HTTP client 
/// and the headless browser. It also creates a test session object that summarizes the outcome of the test operations.
/// </summary>
/// <remarks>
/// The TestAgent class performs the core testing logic of the Xping SDK. It has two methods that can execute test 
/// components that have been added to a container: <see cref="RunAsync(Uri, TestSettings, CancellationToken)"/> and 
/// <see cref="ProbeAsync(Uri, TestSettings, CancellationToken)"/>. The former executes the test components and 
/// creates a test session that summarizes the test operations. The latter serves as a quick check to ensure that the 
/// test components are properly configured and do not cause any errors. The TestAgent class collects various data 
/// related to the test execution. It constructs a <see cref="TestSession"/> object that represents the outcome of the 
/// test operations, which can be serialized, analyzed, or compared. The TestAgent class can be configured with various 
/// settings, such as the timeout, using the <see cref="TestSettings"/> class.
/// <para>
/// <note type="important">
/// Please note that the TestAgent class is designed to be used with a dependency injection system and should not be 
/// instantiated by the user directly. Instead, the user should register the TestAgent class in the dependency injection 
/// container using one of the supported methods, such as the 
/// <see cref="DependencyInjection.DependencyInjectionExtension.AddTestAgent(IServiceCollection)"/> extension method for 
/// the IServiceCollection interface. This way, the TestAgent class can be resolved and injected into other classes that 
/// depend on it.
/// </note>
/// <example>
/// <code>
/// Host.CreateDefaultBuilder(args)
///     .ConfigureServices((services) =>
///     {
///         services.AddHttpClientFactory();
///         services.AddTestAgent(agent =>
///         {
///             agent.UploadToken = "--- Your Upload Token ---";
///         });
///     });
/// </code>
/// </example>
/// </para>
/// </remarks>
public sealed class TestAgent : IDisposable
{
    private bool _disposedValue;
    private Guid _uploadToken;
    private string? _apiKey;

    private readonly IServiceProvider _serviceProvider;

    // Asynchronous task that detects the current execution location.
    // This task is scheduled to run in the background and will not block the test execution.
    private readonly Lazy<Task<TestLocation?>> _locationDetectionTask;

    // Lazy initialization of a shared pipeline container that is thread-safe. This ensures that only a single instance
    // of Pipeline is created and shared across all threads, and it is only created when it is first accessed and
    // the property InstantiatePerThread is disabled.
    private readonly Lazy<Pipeline> _sharedContainer =
        new(valueFactory: () => new Pipeline($"Pipeline#Shared"), isThreadSafe: true);

    // ThreadLocal is used to provide a separate instance of the pipeline container for each thread.
    private readonly ThreadLocal<Pipeline> _container = new(valueFactory: () =>
        new Pipeline($"Pipeline#Thread[{Thread.CurrentThread.Name}:{Environment.CurrentManagedThreadId}]"));

    /// <summary>
    /// Controls whether the TestAgent's pipeline container object should be instantiated for each thread separately.
    /// When set to true, each thread will have its own instance of the pipeline container. Default is <c>true</c>.
    /// </summary>
    public bool InstantiatePerThread { get; set; } = true;

    /// <summary>
    /// Gets or sets the upload token that links the TestAgent's results to the project configured on the server.
    /// This token facilitates the upload of testing sessions to the server for further analysis. If set to <c>null</c>,
    /// no uploads will occur. Default is <c>null</c>.
    /// </summary>
    public string? UploadToken
    {
        get => _uploadToken.ToString();
        set => _uploadToken = !string.IsNullOrWhiteSpace(value) &&
                              Guid.TryParse(value, out Guid result)
            ? result
            : Guid.Empty;
    }

    /// <summary>
    /// Gets or sets the API key used for authentication with Xping services when uploading data.
    /// If not explicitly set, the value is read from the XPING_API_KEY environment variable.
    /// This key is used to authenticate HTTP requests by adding it as the "x-api-key" header.
    /// </summary>
    public string? ApiKey
    {
        get => _apiKey ?? Environment.GetEnvironmentVariable("XPING_API_KEY");
        set => _apiKey = value;
    }

    /// <summary>
    /// Retrieves the <see cref="Pipeline"/> instance that serves as the execution container.
    /// </summary>
    /// <remarks>
    /// This property provides access to the appropriate <see cref="Pipeline"/> instance based on the current 
    /// configuration. If configured for per-thread instantiation, it returns a thread-specific <see cref="Pipeline"/> 
    /// instance, ensuring thread safety. In a shared configuration, it provides a common <see cref="Pipeline"/> 
    /// instance for all threads.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This exception is thrown if a per-thread <see cref="Pipeline"/> instance is not available, which 
    /// could indicate an issue with its construction or an attempt to access it post-disposal.
    /// </exception>
    public Pipeline Container => InstantiatePerThread
        ? _container.Value ?? throw new InvalidOperationException(
            "The Pipeline instance for the current thread could not be retrieved. This may occur if the Pipeline " +
            "constructor threw an exception or if the ThreadLocal instance was accessed after disposal.")
        : _sharedContainer.Value;

    /// <summary>
    /// Occurs when uploading the test session fails.
    /// </summary>
    public event EventHandler<UploadFailedEventArgs>? UploadFailed;

    /// <summary>
    /// Initializes a new instance of the TestAgent class. For internal use only.
    /// </summary>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    internal TestAgent(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        // Schedule location detection to be performed asynchronously.
        // This is done to avoid blocking the test execution with location detection.
        _locationDetectionTask = new Lazy<Task<TestLocation?>>(
            DetectLocationWithTimeoutAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// This method executes the tests. After the test operations are executed, it constructs a test session that 
    /// represents the outcome of the test operations.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">
    /// Optional <see cref="TestSettings"/> object that contains the settings for the tests.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel the validation process.</param>
    /// <returns>
    /// Returns a Task&lt;TestSession&gt; object that represents the asynchronous outcome of a testing operation.
    /// </returns>
    public async Task<TestSession> RunAsync(
        Uri url,
        TestSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        using var instrumentation = new InstrumentationTimer(startStopwatch: false);
        var pipeline = Container; // Capture the pipeline instance for this test execution
        var context = CreateTestContext(instrumentation, pipeline);
        var metadata = await GetCurrentTestMetadataAsync().ConfigureAwait(false);

        var testSession = await ExecuteTestAsync(url, settings, context, metadata, cancellationToken)
            .ConfigureAwait(false);

        if (_uploadToken != Guid.Empty)
        {
            await HandleSessionUploadAsync(testSession, context.SessionUploader, cancellationToken)
                .ConfigureAwait(false);
        }

        return testSession;
    }

    /// <summary>
    /// Asynchronously probes a test component registered in the current instance of the Container using the specified 
    /// URL and settings.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="cancellationToken">An optional CancellationToken object that can be used to cancel the 
    /// validation process.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating 
    /// whether the probe was successful or not.</returns>
    public async Task<bool> ProbeAsync(
        Uri url,
        TestSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            // Execute the test operation by invoking the ProbeAsync method of the Container class.
            return await Container
                .ProbeAsync(url, settings, _serviceProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Cleanup tests components from the container.
    /// </summary>
    /// <remarks>
    /// This method is called to clean up the test components. It ensures that the component collection is emptied, 
    /// preventing cross-contamination of state between tests.
    /// </remarks>
    public void Cleanup()
    {
        Container.Clear();
    }

    /// <summary>
    /// Releases the resources used by the TestAgent.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
    }

    /// <summary>
    /// Releases the resources used by the TestAgent.
    /// </summary>
    /// <param name="disposing">A flag indicating whether to release the managed resources.</param>
    private void Dispose(bool disposing)
    {
        if (_disposedValue)
            return;

        if (disposing)
        {
            Cleanup();
            _container.Dispose();

            try
            {
                if (_locationDetectionTask.IsValueCreated && !_locationDetectionTask.Value.IsCompleted)
                {
                    // Wait for location detection to complete.
                    _locationDetectionTask.Value.Wait(TimeSpan.FromMilliseconds(3100));
                }
            }
            catch (Exception)
            {
                // Ignore exceptions during disposal
            }
            finally
            {
                if (_locationDetectionTask.IsValueCreated)
                {
                    // Dispose the location detection task if it was created
                    _locationDetectionTask.Value.Dispose();
                }
            }
        }

        _disposedValue = true;
    }

    /// <summary>
    /// Creates a new <see cref="TestContext"/> instance using the provided instrumentation and services from the 
    /// service provider.
    /// </summary>
    /// <remarks>
    /// This method initializes a <see cref="TestContext"/> object, which encapsulates the environment in which a test 
    /// runs, providing access to services such as session building and progress reporting.
    /// </remarks>
    /// <param name="instrumentation">
    /// An IInstrumentation object that provides mechanisms for monitoring and interacting with the test execution 
    /// process.
    /// </param>
    /// <param name="pipeline">
    /// The pipeline instance to be used for the entire test execution to ensure consistency across async operations.
    /// </param>
    /// <returns>
    /// A <see cref="TestContext"/> object configured with the necessary services and instrumentation for test 
    /// execution.
    /// </returns>
    private TestContext CreateTestContext(IInstrumentation instrumentation, Pipeline pipeline)
    {
        var context = new TestContext(
            sessionBuilder: _serviceProvider.GetRequiredService<ITestSessionBuilder>(),
            instrumentation: instrumentation,
            sessionUploader: _serviceProvider.GetRequiredService<ITestSessionUploader>(),
            pipeline: pipeline,
            progress: _serviceProvider.GetService<IProgress<TestStep>>());

        return context;
    }

    /// <summary>
    /// Executes the test operations and builds the test session.
    /// </summary>
    /// <param name="url">The URL being tested.</param>
    /// <param name="settings">The test settings.</param>
    /// <param name="context">The test context.</param>
    /// <param name="metadata">The test metadata information.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed test session.</returns>
    private async Task<TestSession> ExecuteTestAsync(
        Uri url,
        TestSettings? settings,
        TestContext context,
        TestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var sessionBuilder = context.SessionBuilder;

        try
        {
            InitializeExecution(url, context, metadata);

            await context.Pipeline
                .HandleAsync(url, settings ?? new TestSettings(), context, _serviceProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            sessionBuilder.Build(agent: this, error: Errors.ExceptionError(ex));
        }

        return sessionBuilder.GetTestSession();
    }

    /// <summary>
    /// Initializes and updates the execution context.
    /// </summary>
    /// <param name="url">The URL being tested.</param>
    /// <param name="context">The test context.</param>
    /// <param name="metadata">The test metadata information.</param>
    private void InitializeExecution(Uri url, TestContext context, TestMetadata metadata)
    {
        // Update context with a currently executing component.
        context.UpdateExecutionContext(context.Pipeline);

        // Initiate the test session builder by recording its start time, the URL of the page being validated,
        // and associating it with the current TestContext responsible for maintaining the state of the test
        // execution.
        context.SessionBuilder.Initiate(url, startDate: DateTime.UtcNow, context, _uploadToken, metadata);
    }

    /// <summary>
    /// Handles the uploading of the test session if the upload token is configured.
    /// </summary>
    /// <param name="testSession">The test session to upload.</param>
    /// <param name="sessionUploader">The session uploader service.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task HandleSessionUploadAsync(
        TestSession testSession,
        ITestSessionUploader sessionUploader,
        CancellationToken cancellationToken)
    {
        var result = await sessionUploader.UploadAsync(testSession, ApiKey, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccessful)
        {
            OnUploadFailed(new UploadFailedEventArgs(testSession, result));
        }
    }

    /// <summary>
    /// Raises the <see cref="UploadFailed"/> event.
    /// </summary>
    /// <param name="e">The event data.</param>
    private void OnUploadFailed(UploadFailedEventArgs e)
    {
        UploadFailed?.Invoke(this, e);
    }

    private async Task<TestMetadata> GetCurrentTestMetadataAsync()
    {
        var stackTrace = new StackTrace();

        // Look for the test method in the stack trace
        MethodInfo? testMethod = null;
        Type? testClass = null;

        for (int i = 1; i < stackTrace.FrameCount; i++)
        {
            var frame = stackTrace.GetFrame(i);
            var method = frame?.GetMethod();

            if (method != null && IsTestMethod(method))
            {
                testMethod = method as MethodInfo;
                testClass = method.DeclaringType;
                break;
            }
        }

        var location = await _locationDetectionTask.Value.ConfigureAwait(false);

        // If we found a test method, return detailed metadata
        if (testMethod != null && testClass != null)
        {
            var methodAttributes = testMethod.GetCustomAttributes().ToList();
            var classAttributes = testClass.GetCustomAttributes().ToList();
            var testDescription = ExtractTestDescription(methodAttributes);
            var xpingIdentifier = ExtractXpingIdentifier(methodAttributes, classAttributes);
            var (processName, processArguments) = GetProcessNameAndArguments();

            TestMetadata testMethodMetadata = new()
            {
                MethodName = testMethod.Name,
                ClassName = testClass.Name,
                Namespace = testClass.Namespace ?? "Unknown",
                ProcessName = processName,
                ProcessArguments = processArguments,
                ProcessId = Environment.ProcessId,
                ClassAttributeNames = [.. classAttributes.Select(attr => attr.GetType().Name)],
                MethodAttributeNames = [.. methodAttributes.Select(attr => attr.GetType().Name)],
                TestDescription = testDescription,
                Location = location
            };

            testMethodMetadata.UpdateXpingIdentifier(xpingIdentifier ?? CreateXpingIdentifier(testMethodMetadata));

            return testMethodMetadata;
        }

        // Fallback to process information when no test method is found
        var (fallbackProcessName, fallbackProcessArguments) = GetProcessNameAndArguments();
        TestMetadata metadata = new()
        {
            MethodName = string.Empty,
            ClassName = string.Empty,
            Namespace = string.Empty,
            ProcessName = fallbackProcessName,
            ProcessArguments = fallbackProcessArguments,
            ProcessId = Environment.ProcessId,
            ClassAttributeNames = [],
            MethodAttributeNames = [],
            TestDescription = null,
            Location = location
        };

        // Generate a fallback Xping identifier based on the available metadata
        metadata.UpdateXpingIdentifier(CreateXpingIdentifier(metadata));

        return metadata;
    }

    /// <summary>
    /// Extracts the test description from method attributes.
    /// </summary>
    /// <param name="methodAttributes">The method attributes to examine.</param>
    /// <returns>The test description if found; otherwise, null.</returns>
    private static string? ExtractTestDescription(IEnumerable<Attribute> methodAttributes)
    {
        var testAttributes = methodAttributes.Where(attr =>
            attr.GetType().Name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            attr.GetType().Name.Contains("Fact", StringComparison.OrdinalIgnoreCase) ||
            attr.GetType().Name.Contains("Theory", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var attribute in testAttributes)
        {
            var descriptionProperty = attribute.GetType().GetProperty("Description");
            if (descriptionProperty != null)
            {
                var description = descriptionProperty.GetValue(attribute) as string;
                if (!string.IsNullOrEmpty(description))
                    return description;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the XpingIdentifier from method and class attributes.
    /// Method-level XpingAttribute takes precedence over class-level XpingAttribute.
    /// </summary>
    /// <param name="methodAttributes">The collection of method attributes.</param>
    /// <param name="classAttributes">The collection of class attributes.</param>
    /// <returns>The XpingIdentifier if found; otherwise, null.</returns>
    private static string? ExtractXpingIdentifier(
        IEnumerable<Attribute> methodAttributes,
        IEnumerable<Attribute> classAttributes)
    {
        // Check method-level XpingAttribute first (higher priority)
        var methodXpingAttribute = methodAttributes.OfType<XpingAttribute>().FirstOrDefault();
        if (methodXpingAttribute != null)
        {
            return methodXpingAttribute.Identifier;
        }

        // Fall back to class-level XpingAttribute
        var classXpingAttribute = classAttributes.OfType<XpingAttribute>().FirstOrDefault();
        return classXpingAttribute?.Identifier;
    }

    /// <summary>
    /// Creates a unique and deterministic string identifier based on test metadata.
    /// This method serves as a fallback when XpingIdentifier cannot be extracted from attributes.
    /// It always returns the same identifier for the same test on the same machine, regardless of time or process.
    /// </summary>
    /// <param name="metadata">The test metadata containing information about the test method, class, and execution context.</param>
    /// <returns>A deterministic string identifier that uniquely identifies the test execution context.</returns>
    private static string CreateXpingIdentifier(TestMetadata? metadata)
    {
        if (metadata == null)
        {
            return "Unknown#Fallback";
        }

        // Build components for the identifier using only stable, deterministic values
        var components = new List<string>();

        // Add namespace, class, and method if available (test method context)
        if (!string.IsNullOrEmpty(metadata.Namespace))
            components.Add(metadata.Namespace);

        if (!string.IsNullOrEmpty(metadata.ClassName))
            components.Add(metadata.ClassName);

        if (!string.IsNullOrEmpty(metadata.MethodName))
            components.Add(metadata.MethodName);

        // Add stable location information if available (machine/environment context)
        if (metadata.Location != null)
        {
            if (!string.IsNullOrEmpty(metadata.Location.Country))
                components.Add(metadata.Location.Country);

            if (!string.IsNullOrEmpty(metadata.Location.Region))
                components.Add(metadata.Location.Region);

            // Only add city if it provides meaningful distinction
            if (!string.IsNullOrEmpty(metadata.Location.City))
                components.Add(metadata.Location.City);
        }

        // If we don't have test method info, create a generic fallback
        if (components.Count == 0)
        {
            return "Generic#Test";
        }

        try
        {
            // Join components with a delimiter
            var identifierString = string.Join("::", components);

            // Create a deterministic hash for consistent results
            // Use a stack-allocated buffer for UTF8 encoding to avoid heap allocations
            Span<byte> utf8Bytes = stackalloc byte[System.Text.Encoding.UTF8.GetMaxByteCount(identifierString.Length)];
            int bytesWritten = System.Text.Encoding.UTF8.GetBytes(identifierString.AsSpan(), utf8Bytes);

            // Hash the UTF8 bytes directly using the modern HashData API
            var hashBytes = SHA256.HashData(utf8Bytes[..bytesWritten]);
            // Convert to base64 for compact representation (first 12 characters for readability)
            var base64Hash = Convert.ToBase64String(hashBytes)
                .TrimEnd('=')[..Math.Min(12, Convert.ToBase64String(hashBytes).TrimEnd('=').Length)];

            // Create readable prefix
            string readablePrefix;
            if (metadata.IsTestMethod)
            {
                readablePrefix = $"{metadata.ClassName}.{metadata.MethodName}";
            }
            else
            {
                readablePrefix = "Test";
            }

            return $"{readablePrefix}#{base64Hash}";
        }
        catch (Exception)
        {
            // Ultimate fallback - still deterministic based on test method
            if (metadata.IsTestMethod)
            {
                return $"{metadata.ClassName}.{metadata.MethodName}#Fallback";
            }

            return "Test#Fallback";
        }
    }

    /// <summary>
    /// Determines if a method is a test method by checking for common test framework attributes.
    /// This method is framework-agnostic and works with NUnit, MSTest, xUnit, etc.
    /// </summary>
    /// <param name="method">The method to check.</param>
    /// <returns>True if the method appears to be a test method; otherwise, false.</returns>
    private static bool IsTestMethod(MethodBase method)
    {
        var attributes = method.GetCustomAttributes();

        return attributes.Any(attr =>
            attr.GetType().Name.Equals("TestAttribute", StringComparison.OrdinalIgnoreCase) ||
            attr.GetType().Name.Equals("FactAttribute", StringComparison.OrdinalIgnoreCase) ||
            attr.GetType().Name.Equals("TheoryAttribute", StringComparison.OrdinalIgnoreCase) ||
            attr.GetType().Name.Equals("TestMethodAttribute", StringComparison.OrdinalIgnoreCase) ||
            attr.GetType().FullName?.Contains("TestAttribute", StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Gets the process name and arguments separately for the current process.
    /// This method attempts to retrieve the full command line and separate the process name from its arguments.
    /// </summary>
    /// <remarks>
    /// On macOS and Linux systems, this method uses platform-specific approaches to retrieve
    /// the command line arguments:
    /// - macOS: Uses the 'ps' command to get the command line
    /// - Linux: Reads from the /proc/[pid]/cmdline file
    /// - Windows: Uses Process.GetCurrentProcess() and Environment.GetCommandLineArgs()
    /// 
    /// The method returns only the filename (not the full path) to make the output more readable.
    /// </remarks>
    /// <returns>A tuple containing the process name and arguments as separate strings.</returns>
    private static (string ProcessName, string ProcessArguments) GetProcessNameAndArguments()
    {
        var fullCommandLine = GetFullProcessCommandLine();
        
        if (string.IsNullOrWhiteSpace(fullCommandLine))
        {
            return (Process.GetCurrentProcess().ProcessName, string.Empty);
        }

        var parts = fullCommandLine.Split(' ', 2);
        var processName = parts[0];
        var processArguments = parts.Length > 1 ? parts[1] : string.Empty;
        var args = Environment.GetCommandLineArgs();
        if (args == null || args.Length == 0)
        {
            return (Process.GetCurrentProcess().ProcessName, string.Empty);
        }
        var processName = System.IO.Path.GetFileName(args[0]);
        var processArguments = args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : string.Empty;
        return (processName, processArguments);
    }

    /// <summary>
    /// Retrieves the full process command line including arguments.
    /// </summary>
    /// <remarks>
    /// Due to OS limitations on macOS and Unix systems where process names are truncated to 15 chars in the process
    /// table, this method implements platform-specific workarounds to get the complete process name and arguments:
    /// - On macOS: Uses 'ps' command to get the full command line
    /// - On Linux: Reads from /proc/{pid}/cmdline
    /// - On Windows: Gets information via Process.MainModule
    /// This ensures accurate process identification across all supported platforms.
    /// </remarks>
    /// <returns>The full process command line including the executable name and arguments.</returns>
    private static string GetFullProcessCommandLine()
    {
        if (OperatingSystem.IsMacOS())
        {
            var pid = Environment.ProcessId;
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "ps",
                Arguments = $"-p {pid} -o command=",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            string? output = process.StandardOutput.ReadLine();
            process.WaitForExit();

            // Remove a full path from the command, keep only filename and arguments
            if (!string.IsNullOrWhiteSpace(output))
            {
                var parts = output.Trim().Split(' ', 2);
                var fileName = Path.GetFileName(parts[0]);
                return parts.Length > 1 ? $"{fileName} {parts[1]}" : fileName;
            }
            return string.Empty;
        }

        if (OperatingSystem.IsLinux())
        {
            var cmdline = File.ReadAllText($"/proc/{Environment.ProcessId}/cmdline")
                .Replace('\0', ' ')
                .Trim();

            // Remove a full path from the command, keep only filename and arguments
            if (!string.IsNullOrWhiteSpace(cmdline))
            {
                var parts = cmdline.Split(' ', 2);
                var fileName = Path.GetFileName(parts[0]);
                return parts.Length > 1 ? $"{fileName} {parts[1]}" : fileName;
            }
            return string.Empty;
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var process = Process.GetCurrentProcess();

                // Remove a full path from the command, keep only filename and arguments
                var fileName = Path.GetFileName(process.MainModule?.FileName ?? process.ProcessName);
                var args = Environment.GetCommandLineArgs();
                if (args.Length > 1)
                    return $"{fileName} {string.Join(' ', args.Skip(1))}";
                return fileName;
            }
            catch (Exception)
            {
                return Process.GetCurrentProcess().ProcessName;
            }
        }

        return Process.GetCurrentProcess().ProcessName;
    }

    /// <summary>
    /// Detects the current execution location with a timeout to avoid blocking test execution.
    /// </summary>
    /// <returns>TestLocation information or null if detection fails or times out.</returns>
    private async Task<TestLocation?> DetectLocationWithTimeoutAsync()
    {
        try
        {
            var locationService = _serviceProvider.GetRequiredService<ILocationDetectionService>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)); // 3 second timeout

            return await locationService.DetectLocationAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when timeout occurs. Return null if location detection times out
            return null;
        }
        catch (Exception ex)
        {
            // Log the exception for debugging purposes, this ensures test execution is not disrupted by 
            // location detection issues
            Debug.WriteLine($"Location detection failed: {ex.Message}");

            return null;
        }
    }
}