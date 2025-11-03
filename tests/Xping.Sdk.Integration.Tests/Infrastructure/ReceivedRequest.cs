/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Integration.Tests.Infrastructure;

using System;
using System.Collections.Generic;

internal sealed class ReceivedRequest
{
    public required string Method { get; init; }
    public required string Url { get; init; }
    public required Dictionary<string, string> Headers { get; init; }
    public string? Body { get; init; }
    public DateTime ReceivedAt { get; init; }
}
