using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;

namespace Xping.Sdk.Core.Session;


/// <summary>
/// The ITestSessionBuilder interface is used to build test sessions. It provides methods to initialize the test session 
/// builder with the specified URL and start date, get a value indicating whether the test session has failed, get the 
/// property bag that stores key-value pairs of items that can be referenced later in the pipeline, and build a test 
/// step with the specified component, instrumentation log, and error or exception.
/// </summary>
public interface ITestSessionBuilder
{
    /// <summary>
    /// Initializes the test session builder with the specified URL, start date and <see cref="TestContext"/> object.
    /// </summary>
    /// <param name="url">The URL to be used for the test session.</param>
    /// <param name="startDate">The start date of the test session.</param>
    /// <param name="context">The context responsible for maintaining the state of the test execution.</param>
    /// <returns>The initialized test session builder.</returns>
    ITestSessionBuilder Initiate(Uri url, DateTime startDate, TestContext context);

    /// <summary>
    /// Gets a value indicating whether the test session has failed.
    /// </summary>
    bool HasFailed { get; }

    /// <summary>
    /// Gets a read only collection of test steps associated with the current instance of the test session builder.
    /// </summary>
    IReadOnlyCollection<TestStep> Steps { get; }

    /// <summary>
    /// Builds a test session that has been declined by the <see cref="TestAgent"/>. 
    /// </summary>
    /// <param name="agent">A test agent object wich declined test session.</param>
    /// <param name="error">The error to be used for the test session as decline reason.</param>
    void Build(TestAgent agent, Error error);

    /// <summary>
    /// Builds a test session property bag with the speicified <see cref="PropertyBagKey"/> and 
    /// <see cref="ISerializable"/> derived type as a property bag value. 
    /// </summary>
    /// <param name="key">The property bag key that identifies the test session data.</param>
    /// <param name="value">The property bag value that contains the test session data.</param>
    /// <returns>An instance of the current ITestSessionBuilder that can be used to build the test session.</returns>
    ITestSessionBuilder Build(PropertyBagKey key, IPropertyBagValue value);

    /// <summary>
    /// Constructs a successful <see cref="TestStep"/> instance, integrating it with the corresponding test component.
    /// This instance is also synchronized with the instrumentation timer managed by the <see cref="TestContext"/>,
    /// ensuring accurate tracking of execution metrics.
    /// </summary>
    /// <returns>The built test step.</returns>
    TestStep Build();

    /// <summary>
    /// Constructs a failed <see cref="TestStep"/> instance, integrating it with the corresponding test component.
    /// This instance is also synchronized with the instrumentation timer managed by the <see cref="TestContext"/>,
    /// ensuring accurate tracking of execution metrics.
    /// </summary>
    /// <param name="error">The error to be used for the failed test step.</param>
    /// <returns>The built test step.</returns>
    TestStep Build(Error error);

    /// <summary>
    /// Constructs a failed <see cref="TestStep"/> instance, integrating it with the corresponding test component.
    /// This instance is also synchronized with the instrumentation timer managed by the <see cref="TestContext"/>,
    /// ensuring accurate tracking of execution metrics.
    /// </summary>
    /// <param name="exception">The exception to be used for the failed test step.</param>
    /// <returns>The built test step.</returns>
    TestStep Build(Exception exception);

    /// <summary>
    /// Gets the test session.
    /// </summary>
    /// <param name="uploadToken">
    /// The upload token that links the TestAgent's results to the project configured on the server.
    /// </param>
    /// <returns>The test session.</returns>
    TestSession GetTestSession(Guid uploadToken);
}

