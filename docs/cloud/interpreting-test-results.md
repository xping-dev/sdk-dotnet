# Interpreting Test Results

Once you've integrated the Xping SDK and your tests are running, Xping Cloud provides rich insights into test reliability. This guide explains how to read the scores and make informed decisions about your tests. For the data the SDK records, see [Data Xping collects](../configuration/collected-data.md).

---

## Reading Confidence Scores

**High Scores (0.75+)**: These tests are reliable. When they fail, investigate the failure as it's likely a real bug.

**Medium Scores (0.60-0.74)**: These tests show some inconsistency. Monitor them and watch for trends:
- If the trend is improving (↗️ ⬆️), your fixes are working
- If stable (➡️), the test is consistently moderately reliable
- If degrading (↘️ ⬇️), prioritize investigation

**Low Scores (<0.60)**: These tests need attention, but a low score doesn't automatically mean the test is flaky. A test that consistently fails due to a genuine bug will also have a low confidence score. Xping distinguishes between consistently broken tests and flaky ones - only tests that exhibit flakiness patterns (inconsistent pass/fail behavior, retry dependencies, environment-specific issues) appear in the "Flaky Tests" tab. Consistently failing tests will show low scores but won't be flagged as flaky until they demonstrate unstable behavior.

> **Related**: For details on what confidence scores are and how they're calculated, see [Understanding Confidence Scores](confidence-scores.md).

---

## Confidence Level Matters

Always consider both the score and the confidence level:

| Scenario | Interpretation | Action |
|----------|---------------|--------|
| 0.95 (Very High) | Highly reliable, well-established pattern | Trust it completely |
| 0.95 (Low) | Looks good now, but limited data | Monitor as more data comes in |
| 0.45 (Very High) | Definitely flaky, strong evidence | Fix urgently |
| 0.45 (Low) | Possibly flaky, need more data | Let it run more, then reassess |

---

## Success Rate vs. Confidence Score

These are different metrics:

- **Success Rate**: Simple pass/fail percentage
  - Example: 85% means 85 of last 100 runs passed

- **Confidence Score**: Holistic reliability assessment
  - Considers pass rate **plus** execution consistency, retry patterns, environment behavior, etc.
  - Example: A test with 100% pass rate but high retry rate will have a lower confidence score

**Why the difference matters:**
A test can have a high success rate but low confidence score if it frequently needs retries or shows inconsistent execution times. Xping flags this because it indicates underlying instability.

---

## Trend Indicators

Trend arrows show score movement:

**Improving Tests** (↗️ ⬆️):
```
Week 1: 0.55 → Week 2: 0.72 → Week 3: 0.88
Your fixes are working! Continue monitoring.
```

**Stable Tests** (➡️):
```
Week 1: 0.45 → Week 2: 0.47 → Week 3: 0.46
Chronically flaky - not improving. Consider rewrite.
```

**Degrading Tests** (↘️ ⬇️):
```
Week 1: 0.95 → Week 2: 0.82 → Week 3: 0.71
Something changed recently. Investigate code changes.
```

---

## Quick Decision Framework

Use this framework to decide what to do based on confidence scores:

| Confidence Score | Confidence Level | Action |
|-----------------|------------------|--------|
| **0.75+** | Any | Trust failures as real bugs. Investigate failures normally. |
| **0.60-0.74** | Medium+ | Monitor trends. If degrading, investigate. |
| **0.60-0.74** | Low | Run more tests to get better data before acting. |
| **<0.60** | High/Very High | Fix urgently - strong evidence of flakiness. |
| **<0.60** | Medium | Investigate and likely fix - moderate evidence. |
| **<0.60** | Low | Gather more data, then investigate. |

---

## See Also

- [Understanding Confidence Scores](confidence-scores.md) - Learn how scores are calculated
- [Identifying Flaky Tests](../flaky-tests/finding-flaky-tests.md) - Use Xping Cloud to find unreliable tests
- [Fixing Flaky Tests](../flaky-tests/patterns-and-fixes.md) - Take action on low-confidence tests
