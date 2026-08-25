/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Signatures;

/// <summary>
/// The namespace roots a stack frame is not worth signing a failure with.
/// </summary>
/// <remarks>
/// <para>
/// The specification asks for frames belonging to the test project or its transitive project
/// references. That relation cannot be recovered from a session: a stack trace is untyped text with
/// no per-frame assembly, and the project graph lives in files that analysis is not allowed to read
/// and that need not exist on the machine running the report at all.
/// </para>
/// <para>
/// So the question is inverted. Everything is a user frame unless it belongs to a test framework, an
/// assertion or mocking library, or the runtime itself. Keeping the rule negative is what preserves
/// frames from the code under test — <c>MyApp.Services.OrderService.Place</c> is where the failure
/// actually happened, and a positive rule keyed on the test assembly's own name would discard it.
/// </para>
/// <para>
/// The trade is that an unlisted third-party library reads as a user frame. That costs some
/// signature precision; the opposite error costs the diagnostic entirely.
/// </para>
/// <para>
/// One entry is not a namespace at all. The CLR emits a reflection invoke stub named
/// <c>InvokeStub_{Type}</c> in the global namespace, and whether it appears is a property of how many
/// times the runtime has already invoked that method — interpreted the first time, through an emitted
/// stub afterwards. Two runs of the same failing method therefore produce traces that differ by one
/// frame, which is enough to give them different signatures and stop them grouping. It is runtime
/// scaffolding by any reading, and it is filtered as such.
/// </para>
/// <para>
/// <c>Xping.</c> is deliberately absent. In this repository Xping is the code under test, and
/// denying it would blind the report to its own failures.
/// </para>
/// </remarks>
internal static class FrameworkNamespaces
{
    private static readonly string[] Roots =
    [
        // Assertion and mocking libraries — the frame that threw, never the frame that is wrong.
        "AutoFixture.",
        "Castle.",
        "FakeItEasy.",
        "FluentAssertions.",
        "Moq.",
        "NSubstitute.",
        "Shouldly.",

        // Test frameworks and their runners.
        "MSTest.",
        "NUnit.",
        "TestStack.",
        "Xunit.",
        "xunit.",

        // Tooling that appears in a trace when a test runs under an IDE or coverage tool.
        "Coverlet.",
        "JetBrains.",

        // The runtime and the platform.
        "Internal.",
        "Microsoft.",
        "System.",

        // Reflection invoke stubs the CLR emits into the global namespace. See the remarks above:
        // their presence varies between runs of the same method, so a frame that survived here would
        // make a signature depend on how warm the runtime was.
        "InvokeStub_",
    ];

    /// <summary>
    /// Returns whether a frame belongs to the framework rather than to the code under test.
    /// </summary>
    /// <param name="frame">The frame's method signature, as extracted from the stack trace.</param>
    /// <returns><see langword="true"/> when the frame should not contribute to a signature.</returns>
    public static bool IsFramework(string frame)
    {
        foreach (string root in Roots)
        {
            if (frame.StartsWith(root, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
