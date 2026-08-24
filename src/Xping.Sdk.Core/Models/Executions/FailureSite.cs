/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// Where in the test lifecycle a failing execution actually failed.
/// </summary>
/// <remarks>
/// <para>
/// A test that disagreed with an assertion and a test knocked over by a broken <c>[SetUp]</c> are both
/// recorded as <see cref="TestOutcome.Failed"/>, and they are different defects with different fixes.
/// The second one is not really N failing tests at all: it is one broken lifecycle member, reported
/// once per test that tried to use it. Without this field the report can say those N tests fail the
/// same way, but not that the fixture is the thing to fix.
/// </para>
/// <para>
/// Every value is declared, including those no adapter can currently detect, because the enum is part
/// of the JSON contract — a consumer written against it should not need updating each time a framework
/// starts exposing a site it previously hid. Which values are reachable on which framework is recorded
/// in <c>docs/known-limitations.md</c>, and is a property of the test frameworks rather than of this
/// enum.
/// </para>
/// <para>
/// Use <see cref="FailureSiteExtensions.IsLifecycle"/> rather than comparing against individual values
/// when asking whether shared lifecycle code was at fault.
/// </para>
/// </remarks>
public enum FailureSite
{
    /// <summary>
    /// The execution failed, and the adapter could not determine where.
    /// </summary>
    /// <remarks>
    /// Deliberately the default. A framework that reports no stack trace, or an adapter that cannot
    /// read one, must say so rather than fall through to <see cref="TestBody"/> — claiming the body
    /// failed is an assertion about the code under test, and it would be made without evidence for
    /// every failure the adapter failed to classify.
    /// </remarks>
    Unknown = 0,

    /// <summary>The test method body.</summary>
    TestBody = 1,

    /// <summary>
    /// Per-test setup: NUnit <c>[SetUp]</c>, MSTest <c>[TestInitialize]</c>, an xUnit test class
    /// constructor or <c>IAsyncLifetime.InitializeAsync</c>.
    /// </summary>
    TestSetup = 2,

    /// <summary>
    /// Per-test teardown: NUnit <c>[TearDown]</c>, MSTest <c>[TestCleanup]</c>, an xUnit test class
    /// <c>Dispose</c> or <c>DisposeAsync</c>.
    /// </summary>
    TestTeardown = 3,

    /// <summary>
    /// One-time fixture setup: NUnit <c>[OneTimeSetUp]</c>, MSTest <c>[ClassInitialize]</c>, or
    /// construction of an xUnit <c>IClassFixture&lt;T&gt;</c> or <c>ICollectionFixture&lt;T&gt;</c>.
    /// </summary>
    FixtureSetup = 4,

    /// <summary>
    /// One-time fixture teardown: NUnit <c>[OneTimeTearDown]</c>, MSTest <c>[ClassCleanup]</c>, or
    /// disposal of an xUnit fixture.
    /// </summary>
    FixtureTeardown = 5,

    /// <summary>
    /// Assembly-wide setup: a NUnit <c>[SetUpFixture]</c>'s one-time setup, or MSTest
    /// <c>[AssemblyInitialize]</c>.
    /// </summary>
    AssemblySetup = 6,

    /// <summary>
    /// Assembly-wide teardown: a NUnit <c>[SetUpFixture]</c>'s one-time teardown, or MSTest
    /// <c>[AssemblyCleanup]</c>.
    /// </summary>
    AssemblyTeardown = 7
}
