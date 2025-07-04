/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Runtime.Serialization;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Session;

/// <summary>
/// TestSessionBuilder is a concrete implementation of the <see cref="ITestSessionBuilder"/> interface that is used to 
/// build test sessions.
/// </summary>
public class TestSessionBuilder : ITestSessionBuilder
{
    private readonly List<TestStep> _steps = [];
    private readonly Dictionary<ITestComponent, int> _componentStepCounts = [];

    private Uri? _url;
    private DateTime _startDate = DateTime.MinValue;
    private Guid _uploadToken = Guid.Empty;
    private TestContext? _context;
    private Error? _error;
    private PropertyBag<IPropertyBagValue>? _propertyBag;

    /// <summary>
    /// Gets a value indicating whether the test session has failed.
    /// </summary>
    public bool HasFailed => _steps.Any(step => step.Result == TestStepResult.Failed);

    /// <summary>
    /// Gets a collection of test steps associated with the current instance of the test session builder.
    /// </summary>
    public IReadOnlyCollection<TestStep> Steps => _steps.AsReadOnly();

    /// <summary>
    /// Gets the test component associated with the testing context.
    /// </summary>
    protected ITestComponent Component => 
        _context.RequireNotNull(nameof(_context)).CurrentComponent.RequireNotNull(nameof(Component));

    /// <summary>
    /// Gets the instrumentation timer associated with the testing context.
    /// </summary>
    protected IInstrumentation Instrumentation =>
        _context.RequireNotNull(nameof(_context)).Instrumentation;

    /// <summary>
    /// Initializes the test session builder with the specified start time, the URL of the page being validated,
    /// and associating it with the current TestContext responsible for maintaining the state of the test execution.
    /// </summary>
    /// <param name="url">The URL to be used for the test session.</param>
    /// <param name="startDate">The start date of the test session.</param>
    /// <param name="context">The context responsible for maintaining the state of the test execution.</param>
    /// <param name="uploadToken">
    /// The upload token that links the TestAgent's results to the project configured on the server.
    /// </param>
    /// <returns>The initialized test session builder.</returns>
    public ITestSessionBuilder Initiate(Uri url, DateTime startDate, TestContext context, Guid uploadToken)
    {
        _url = url;
        _startDate = startDate;
        _uploadToken = uploadToken;
        _context = context.RequireNotNull(nameof(context));

        // Reset internal state.
        _error = null;
        _propertyBag = null;
        _steps.Clear();
        _componentStepCounts.Clear();

        return this;
    }

    /// <summary>
    /// Builds a test session that has been declined by the <see cref="TestAgent"/>. 
    /// </summary>
    /// <param name="agent">A test agent object which declined a test session.</param>
    /// <param name="error">The error to be used for the test session as a decline reason.</param>
    public void Build(TestAgent agent, Error error)
    {
        _error = error.RequireNotNull(nameof(error));
    }

    /// <summary>
    /// Builds a test session property bag with the specified <see cref="PropertyBagKey"/> and 
    /// <see cref="ISerializable"/> derived type as a property bag value. 
    /// </summary>
    /// <param name="key">The property bag key that identifies the test session data.</param>
    /// <param name="value">The property bag value that contains the test session data.</param>
    /// <returns>An instance of the current ITestSessionBuilder that can be used to build the test session.</returns>
    public ITestSessionBuilder Build(PropertyBagKey key, IPropertyBagValue value)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        _propertyBag ??= new PropertyBag<IPropertyBagValue>();
        _propertyBag.AddOrUpdateProperty(key, value);

        return this;
    }

    /// <summary>
    /// Constructs a successful <see cref="TestStep"/> instance, integrating it with the corresponding test component.
    /// This instance is also synchronized with the instrumentation timer managed by the <see cref="TestContext"/>,
    /// ensuring accurate tracking of execution metrics.
    /// </summary>
    /// <returns>The built test step.</returns>
    public TestStep Build()
    {
        // Increment the step count for the given ITestComponent instance to ensure each component step is accurately
        // named based on its number of steps within the test session.
        _componentStepCounts[Component] = 
            _componentStepCounts.TryGetValue(Component, out var count) ? count + 1 : 1;

        var testStep = new TestStep
        {
            Name = Component.Name,
            TestComponentIteration = _componentStepCounts[Component],
            StartDate = Instrumentation.StartTime,
            Duration = Instrumentation.ElapsedTime,
            Type = Component.Type,
            PropertyBag = _propertyBag,
            Result = TestStepResult.Succeeded,
            ErrorMessage = null
        };

        _steps.Add(testStep);

        // Set the property bag to null to force its reconstruction when new data is added.
        _propertyBag = null;

        // Restart the instrumentation timer to measure the elapsed time for a new test step.
        // This ensures that the previous test step's time is not included in the measurement.
        Instrumentation.Restart();

        return testStep;
    }

    /// <summary>
    /// Constructs a failed <see cref="TestStep"/> instance, integrating it with the corresponding test component.
    /// This instance is also synchronized with the instrumentation timer managed by the <see cref="TestContext"/>,
    /// ensuring accurate tracking of execution metrics.
    /// </summary>
    /// <param name="error">The error to be used for the failed test step.</param>
    /// <returns>The built test step.</returns>
    public TestStep Build(Error error)
    {
        ArgumentNullException.ThrowIfNull(error, nameof(error));

        // Increment the step count for the given ITestComponent instance to ensure each component step is accurately
        // named based on its number of steps within the test session.
        _componentStepCounts[Component] = _componentStepCounts.TryGetValue(Component, out int count) ? count + 1 : 1;

        var testStep = new TestStep
        {
            Name = Component.Name,
            TestComponentIteration = _componentStepCounts[Component],
            StartDate = Instrumentation.StartTime,
            Duration = Instrumentation.ElapsedTime,
            Type = Component.Type,
            PropertyBag = _propertyBag,
            Result = TestStepResult.Failed,
            ErrorMessage = error
        };

        _steps.Add(testStep);

        // Set the property bag to null to force its reconstruction when new data is added.
        _propertyBag = null;

        // Restart the instrumentation timer to measure the elapsed time for a new test step.
        // This ensures that the previous test step's time is not included in the measurement.
        Instrumentation.Restart();

        return testStep;
    }

    /// <summary>
    /// Constructs a failed <see cref="TestStep"/> instance, integrating it with the corresponding test component.
    /// This instance is also synchronized with the instrumentation timer managed by the <see cref="TestContext"/>,
    /// ensuring accurate tracking of execution metrics.
    /// </summary>
    /// <param name="exception">The exception to be used for the failed test step.</param>
    /// <returns>The built test step.</returns>
    public TestStep Build(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception, nameof(exception));

        // Increment the step count for the given ITestComponent instance to ensure each component step is accurately
        // named based on its number of steps within the test session.
        _componentStepCounts[Component] = _componentStepCounts.TryGetValue(Component, out int count) ? count + 1 : 1;

        var testStep = new TestStep
        {
            Name = Component.Name,
            TestComponentIteration = _componentStepCounts[Component],
            StartDate = Instrumentation.StartTime,
            Duration = Instrumentation.ElapsedTime,
            Type = Component.Type,
            PropertyBag = _propertyBag,
            Result = TestStepResult.Failed,
            ErrorMessage = Errors.ExceptionError(exception)
        };

        _steps.Add(testStep);

        // Set the property bag to null to force its reconstruction when new data is added.
        _propertyBag = null;

        // Restart the instrumentation timer to measure the elapsed time for a new test step.
        // This ensures that the previous test step's time is not included in the measurement.
        Instrumentation.Restart();

        return testStep;
    }

    /// <summary>
    /// Gets the test session.
    /// </summary>
    /// <returns>The test session.</returns>
    public TestSession GetTestSession()
    {
        try
        {
            if (_error != null)
            {
                throw new ArgumentException(_error.Message);
            }

            var session = new TestSession
            {
                Url = _url!,
                StartDate = _startDate,
                Steps = _steps,
                // Set the test session status to completed to indicate that no further modifications are allowed.
                State = TestSessionState.Completed,
                DeclineReason = null,
                UploadToken = _uploadToken
            };

            return session;
        }
        catch (Exception ex)
        {
            var session = new TestSession
            {
                Url = _url ?? new Uri("/", UriKind.Relative),
                StartDate = _startDate < DateTime.UtcNow ? DateTime.UtcNow : _startDate,
                Steps = _steps,
                State = TestSessionState.Declined,
                DeclineReason = ex.Message,
                UploadToken = _uploadToken
            };

            return session;
        }
    }
}
