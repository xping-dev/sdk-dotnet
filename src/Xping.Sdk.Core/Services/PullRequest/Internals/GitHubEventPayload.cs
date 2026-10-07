/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json.Serialization;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Services.PullRequest.Internals;

/// <summary>
/// The parts of the GitHub Actions event payload (the file at <c>GITHUB_EVENT_PATH</c>) the SDK reads.
/// </summary>
/// <remarks>
/// The serializer's camelCase policy would look for <c>pullRequest</c>, so the snake_case payload
/// names are spelled out.
/// </remarks>
internal sealed class GitHubEventPayload
{
    // ReadOnlySpan<byte> deserialization doesn't skip a BOM the way the stream overloads do.
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    [JsonPropertyName("pull_request")]
    public GitHubEventPullRequest? PullRequest { get; set; }

    [JsonPropertyName("repository")]
    public GitHubEventRepository? Repository { get; set; }

    /// <summary>
    /// Reads the payload at <paramref name="path"/>. Throws when the file can't be read or isn't JSON;
    /// the caller decides how loudly that matters.
    /// </summary>
    internal static GitHubEventPayload? Read(string path, IXpingSerializer serializer)
    {
        ReadOnlySpan<byte> json = File.ReadAllBytes(path);
        if (json.StartsWith(Utf8Bom))
            json = json.Slice(Utf8Bom.Length);

        return serializer.Deserialize<GitHubEventPayload>(json);
    }

    internal sealed class GitHubEventPullRequest
    {
        [JsonPropertyName("head")]
        public GitHubEventCommitRef? Head { get; set; }
    }

    internal sealed class GitHubEventCommitRef
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }

    internal sealed class GitHubEventRepository
    {
        [JsonPropertyName("default_branch")]
        public string? DefaultBranch { get; set; }
    }
}
