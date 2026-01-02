/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Integration.Tests.Infrastructure;

internal sealed class MockApiServerBehavior
{
    public int DelayMs { get; set; }
    public double FailureRate { get; set; }
    public int FailureStatusCode { get; set; } = 500;
    public object? CustomResponse { get; set; }
    public int CustomStatusCode { get; set; } = 200;
}
