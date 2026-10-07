/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud;

/// <summary>
/// A Cloud project (<c>ProjectResponse</c>), with only the members the CLI reads (cli-auth-cli-spec §10.1).
/// </summary>
/// <param name="Id">The project key (<c>externalProjectId</c>) the other routes take.</param>
/// <param name="DisplayName">The name shown in Xping Cloud; for a derived project, the assembly name.</param>
/// <param name="Slug">The project's URL slug.</param>
internal sealed record ProjectSummary(string Id, string? DisplayName, string? Slug);
