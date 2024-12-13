using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Core;

/// <summary>
/// The TestAgent class is the main class that performs the testing logic of the Xping SDK. It runs test components, 
/// for example action or validation steps, such as DnsLookup, IPAddressAccessibilityCheck etc., using the HTTP client 
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
public class TestAgent : IDisposable
{
    private bool _disposedValue;
    private Guid _uploadToken;

    private readonly IServiceProvider _serviceProvider;

    // Lazy initialization of a shared pipeline container that is thread-safe. This ensures that only a single instance
    // of Pipeline is created and shared across all threads, and it is only created when it is first accessed and
    // property InstantiatePerThread is disabled.
    private readonly Lazy<Pipeline> _sharedContainer = new(valueFactory: () =>
    {
        return new Pipeline($"Pipeline#Shared");
    }, isThreadSafe: true);

    // ThreadLocal is used to provide a separate instance of the pipeline container for each thread.
    private readonly ThreadLocal<Pipeline> _container = new(valueFactory: () =>
    {
        // Initialize the container for each thread.
        return new Pipeline($"Pipeline#Thread[{Thread.CurrentThread.Name}:{Environment.CurrentManagedThreadId}]");
    });

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
                   Guid.TryParse(value, out Guid result) ? result : Guid.Empty;
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
    public Pipeline Container => InstantiatePerThread ?
        _container.Value ?? throw new InvalidOperationException(
            "The Pipeline instance for the current thread could not be retrieved. This may occur if the Pipeline " +
            "constructor threw an exception or if the ThreadLocal instance was accessed after disposal.") :
        _sharedContainer.Value;

    /// <summary>
    /// Initializes new instance of the TestAgent class. For internal use only.
    /// </summary>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    internal TestAgent(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// This method executes the tests. After the tests operations are executed, it constructs a test session that 
    /// represents the outcome of the tests operations.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">
    /// Optional <see cref="TestSettings"/> object that contains the settings for the tests.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel the validation process.</param>
    /// <returns>
    /// Returns a Task&lt;TestSession&gt; object that represents the asynchronous outcome of testing operation.
    /// </returns>
    public async Task<TestSession> RunAsync(
        Uri url,
        TestSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        using var instrumentation = new InstrumentationTimer(startStopwatch: false);
        var context = CreateTestContext(instrumentation);
        var sessionBuilder = context.SessionBuilder;

        try
        {
            // Update context with currently executing component.
            context.UpdateExecutionContext(Container);

            // Initiate the test session by recording its start time, the URL of the page being validated,
            // and associating it with the current TestContext responsible for maintaining the state of the test
            // execution.
            sessionBuilder.Initiate(url, startDate: DateTime.UtcNow, context);

            // Execute test operation on the current thread's container or shared instance based on
            // `InstantiatePerThread` property configuration.
            await Container
                .HandleAsync(url, settings ?? new TestSettings(), context, _serviceProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            sessionBuilder.Build(agent: this, error: Errors.ExceptionError(ex));
        }

        TestSession testSession = sessionBuilder.GetTestSession();

        return testSession;
    }

    /// <summary>
    /// Asynchronously probes a test components registered in the current instance of the Container using the specified 
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
            // Execute test operation by invoking the ProbeAsync method of the Container class.
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
    /// Clean up tests components from the container.
    /// </summary>
    /// <remarks>
    /// This method is called to clean up the test components. It ensures that the components collection is emptied, 
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
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the TestAgent.
    /// </summary>
    /// <param name="disposing">A flag indicating whether to release the managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Cleanup();
                _container.Dispose();
            }

            _disposedValue = true;
        }
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
    /// <returns>
    /// A <see cref="TestContext"/> object configured with the necessary services and instrumentation for test 
    /// execution.
    /// </returns>
    protected virtual TestContext CreateTestContext(IInstrumentation instrumentation)
    {
        var context = new TestContext(
            sessionBuilder: _serviceProvider.GetRequiredService<ITestSessionBuilder>(),
            instrumentation: instrumentation,
            progress: _serviceProvider.GetService<IProgress<TestStep>>());

        return context;
    }
}
