/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using System.Text.Json.Serialization;
using Xping.Cli.Analysis;
using Xping.Sdk.Core.Models.Local;

namespace Xping.Cli.Reporting;

/// <summary>
/// Serialises a report for scripting and CI consumption.
/// </summary>
/// <remarks>
/// Versioned so consumers can branch on shape rather than guess. This is a contract other people's
/// pipelines will depend on, so fields are added rather than renamed or repurposed.
/// </remarks>
internal static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Serialises the analysis to JSON.</summary>
    /// <param name="analysis">The analysis to serialise.</param>
    /// <param name="storePath">Store location.</param>
    /// <param name="assembly">Assembly the report was scoped to, if any.</param>
    /// <param name="runs">Runs the analysis covered.</param>
    public static string Write(
        LocalAnalysis analysis,
        string? storePath,
        string? assembly,
        IReadOnlyList<LocalRun> runs)
    {
        var payload = new JsonReport
        {
            SchemaVersion = 1,
            StorePath = storePath,
            Assembly = assembly,
            RunsAnalysed = analysis.RunsAnalysed,
            AssembliesAnalysed = analysis.AssembliesAnalysed,
            HasSufficientHistory = analysis.HasSufficientHistory,
            MinimumRunsForHistory = analysis.MinimumRunsForHistory,
            GeneratedAtUtc = DateTime.UtcNow,
            Runs = runs.Select(r => new JsonRun
            {
                SessionId = r.Header.SessionId,
                StartedAtUtc = r.Header.StartedAtUtc,
                DurationMs = r.Header.DurationMs,
                Assembly = r.Header.Assembly,
                Environment = r.Header.Environment,
                Branch = r.Header.Branch,
                CommitSha = r.Header.CommitSha,
                IsCi = r.Header.IsCi,
                TestCount = r.Records.Count
            }).ToList(),
            UnstableTests = analysis.UnstableTests.Select(ToJson).ToList(),
            ConsistentFailures = analysis.ConsistentFailures.Select(ToJson).ToList()
        };

        return JsonSerializer.Serialize(payload, Options);
    }

    private static JsonFinding ToJson(UnstableTest test) => new()
    {
        Fingerprint = test.Fingerprint,
        Name = test.Name,
        Assembly = test.Assembly,
        Kind = test.Kind.ToString(),
        PassCount = test.PassCount,
        RunCount = test.RunCount,
        History = test.History.ToList(),
        PassedOnAttempt = test.PassedOnAttempt
    };

#pragma warning disable CA1812 // Instantiated by the serializer
    private sealed class JsonReport
    {
        public int SchemaVersion { get; set; }

        public string? StorePath { get; set; }

        public string? Assembly { get; set; }

        public int RunsAnalysed { get; set; }

        public int AssembliesAnalysed { get; set; }

        public bool HasSufficientHistory { get; set; }

        public int MinimumRunsForHistory { get; set; }

        public DateTime GeneratedAtUtc { get; set; }

        public List<JsonRun> Runs { get; set; } = [];

        public List<JsonFinding> UnstableTests { get; set; } = [];

        public List<JsonFinding> ConsistentFailures { get; set; } = [];
    }

    private sealed class JsonRun
    {
        public string? SessionId { get; set; }

        public DateTime StartedAtUtc { get; set; }

        public long DurationMs { get; set; }

        public string? Assembly { get; set; }

        public string? Environment { get; set; }

        public string? Branch { get; set; }

        public string? CommitSha { get; set; }

        public bool IsCi { get; set; }

        public int TestCount { get; set; }
    }

    private sealed class JsonFinding
    {
        public string? Fingerprint { get; set; }

        public string? Name { get; set; }

        public string? Assembly { get; set; }

        public string? Kind { get; set; }

        public int PassCount { get; set; }

        public int RunCount { get; set; }

        /// <summary>Per-run outcomes, oldest first. <c>true</c> is a pass.</summary>
        public List<bool> History { get; set; } = [];

        public int? PassedOnAttempt { get; set; }
    }
#pragma warning restore CA1812
}
