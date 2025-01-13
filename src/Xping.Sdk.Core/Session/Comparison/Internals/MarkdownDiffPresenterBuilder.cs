/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Session.Comparison.Internals;

internal partial class MarkdownDiffPresenterBuilder
{
    public static string Build(MarkdownDiffPresenter presenter, DiffResult result)
    {
        bool empty = result == DiffResult.Empty;

        var builder = new StringBuilder()
            .Append(Include(presenter.IncludeTitle, Title))
            .Append(Include(presenter.IncludeOverview, () => OverviewSection))
            .Append(Include(empty, () => ComparisonCompleted))
            .Append(Include(!empty && presenter.IncludeSummary, () => SummarySection(result)))            
            .Append(Include(!empty && presenter.IncludeDetailedStepByStepAnalysis,
                () => DetailedStepByStepAnalysis(result)))
            .Append(Include(!empty && presenter.IncludeConclusion, () => ConclusionSection));

        return builder.ToString();
    }

    private static readonly HeaderMdDecorator Title = H1(T("Test Session Comparison Report"));
    
    private static List<ITextReport> OverviewSection =>
    [
        H2(T("Overview")),
        P(T("This comparison provides insights into the key differences and similarities between two distinct Test " +
            "Sessions. It serves as a quick reference to gauge the overall performance and outcome of the test " +
            "executions."))
    ];

    private static List<ITextReport> SummarySection(DiffResult result) =>
    [
        TD(T("")),TD(T("Session1")),TD(T("Session2")),TD(T("")),TE(),
        TD(T("---")),TD(T("--------")),TD(T("--------")),TD(T("---")),TE(),
        TD(T("StartDate")),
            TD(C(T(result.Session1.StartDate.ToString(CultureInfo.InvariantCulture)))),
            TD(C(T(result.Session2.StartDate.ToString(CultureInfo.InvariantCulture)))),
            TD(T("The start date marks the initiation of each test session. A comparison of start dates can indicate " +
                 "scheduling efficiency and potential delays in the testing process.")),TE(),
        TD(T("Duration")),
            TD(C(T(result.Session1.Duration.GetFormattedTime(TimeSpanUnit.Millisecond)))),
            TD(C(T(result.Session2.Duration.GetFormattedTime(TimeSpanUnit.Millisecond)))),
            TD(T("The duration of each session is a measure of test efficiency and can highlight performance " +
                 "issues.")),TE(),
        TD(T("Failures")),
            TD(C(T($"{result.Session1.Failures.Count}"))),
            TD(C(T($"{result.Session2.Failures.Count}"))),
            TD(T("Failures are critical to identify areas of concern and focus on improving test stability.")),TE(),
        P(T(""))
    ];

    private static List<ITextReport> DetailedStepByStepAnalysis(DiffResult result) =>
    [
        P(H2(T("Comparative Step-by-Step Analysis"))),
        P(T("A granular analysis of each step allows for a deeper understanding of specific issues and successes " +
            "within the test sessions. This analysis spotlights the differences that were present in both sessions" +
            ", including changes that have been made, as well as identifying the steps " +
            "that were unique to one session, highlighting any that were removed or newly added in the other.")),
        
        TD(T("Test Step")),TD(T("Property Name")),TD(T("Session1")),TD(T("Session2")),TE(),
        TD(T("---------")),TD(T("-------------")),TD(T("--------")),TD(T("--------")),TE(),
        T(string.Join("\r\n", result.Differences
            .Where(d => !d.PropertyName.StartsWith(nameof(TestStep), StringComparison.InvariantCulture))
            .Select(ToSessionChanges))),

        T(ToStepChanges(result.Differences
            .Where(d => d.PropertyName.StartsWith(nameof(TestStep), StringComparison.InvariantCulture))))
    ];

    private static string ToSessionChanges(Difference difference)
    {
        var builder = new StringBuilder()
            .Append(TD(T("")).Generate())
            .Append(TD(C(T(difference.PropertyName))).Generate())
            .Append(TD(C(T(GetFormattedString(difference.Value1)))).Generate())
            .Append(TD(C(T(GetFormattedString(difference.Value2)))).Generate())
            .Append(TD(T("")).Generate());

        return builder.ToString();
    }

    private static string ToStepChanges(IEnumerable<Difference> differences)
    {
        Dictionary<string, IList<Tuple<string, Difference>>> stepsDict = ToStepsDictionary(differences);
        var builder = new StringBuilder();

        foreach (var step in stepsDict)
        {
            foreach (var diff in step.Value)
            {
                builder
                    .Append(TD(B(T(step.Key))).Generate())
                    .Append(TD(C(T(diff.Item1))).Generate())
                    .Append(TD(C(T(GetFormattedString(diff.Item2.Value1)))).Generate())
                    .Append(TD(C(T(GetFormattedString(diff.Item2.Value2)))).Generate())
                    .Append(TE().Generate());
            }
        }

        return builder.ToString();
    }

    private static Dictionary<string, IList<Tuple<string, Difference>>> ToStepsDictionary(
        IEnumerable<Difference> differences)
    {
        return differences
            .Select(diff => new
            {
                Difference = diff,
                Match = TestStepNameRegex().Match(diff.PropertyName)
            })
            .Where(x => x.Match.Success)
            .GroupBy(x => x.Match.Groups[1].Value, x => Tuple.Create(x.Match.Groups[2].Value, x.Difference))
            .ToDictionary(g => g.Key, g => (IList<Tuple<string, Difference>>)[.. g]);
    }

    private static List<ITextReport> ConclusionSection =>
    [
        P(H2(T("Conclusion"))),
        P(T("This detailed comparison sheds light on the performance and reliability of the test sessions, guiding " +
            "future improvements and ensuring the robustness of the system under test."))
    ];

    private static List<ITextReport> ComparisonCompleted =>
    [
        P(H2(T("Comparison Complete"))),
        P(T("The analysis has concluded, and it appears that the two test sessions are identical. There are no " +
            "differences to report. This indicates a consistent performance between the sessions, suggesting " +
            "stability and reliability in the tested components or features.")),
        P(T("If you expected changes or discrepancies, please verify that the correct sessions were compared or " +
            "consider reviewing the test parameters.")),
        P(T("Thank you for using our comparison tool. We're here to assist you with any further testing needs."))
    ];

    private static string Include(bool hasFlag, HeaderMdDecorator report) =>
        hasFlag ? report.Generate() : string.Empty;

    private static string Include(bool hasFlag, Func<IList<ITextReport>> reports) =>
        hasFlag ? reports.Invoke().Aggregate(string.Empty, (str, report) => str += report.Generate()) : string.Empty;

    private static string GetFormattedString(object? value)
    {
        if (value is TimeSpan timeSpan)
        {
            return timeSpan.GetFormattedTime();
        }

        return value?.ToString() ?? string.Empty;
    }

    private static HeaderMdDecorator H1(ITextReport report) => new(report, HeaderType.H1);
    private static HeaderMdDecorator H2(ITextReport report) => new(report, HeaderType.H2);
    private static ParagraphMdDecorator P(ITextReport report) => new(report);
    private static ListMdDecorator L0(ITextReport report) => new(report, nestedLevel: 0);
    private static ListMdDecorator L1(ITextReport report) => new(report, nestedLevel: 1);
    private static BoldTextMdDecorator B(ITextReport report) => new(report);
    private static CodeTextMdDecorator C(ITextReport report) => new(report);
    private static TextReport T(string? text) => new(text ?? string.Empty);
    private static TableDataMdDecorator TD(ITextReport report) => new(report);
    private static TableRowEndMdDecorator TE() => new(T(""));

    [GeneratedRegex(@"TestStep\(""(.*?)""\)\.(.*)")]
    private static partial Regex TestStepNameRegex();
}
