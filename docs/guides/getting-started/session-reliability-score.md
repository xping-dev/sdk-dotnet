# Session Reliability Score

The **session reliability score** is a single number between 0 and 1 that summarises the overall reliability of all tests that ran in a given test session. It gives you an at-a-glance answer to the question: *"How trustworthy was this test run as a whole?"*

---

## How the Score Is Calculated

When a test session completes, Xping aggregates the individual [confidence scores](./understanding-confidence-scores.md) of every test that ran using the following formula:

```
Session Reliability = α × WeightedMean(scores) + (1 − α) × Min(scores)
```

- **WeightedMean** — a confidence-weighted average where tests backed by more execution history carry more weight than newly tracked tests.
- **Min** — the lowest individual confidence score in the session (the worst-performing test).
- **α (alpha)** — a blend factor between 0 and 1 that controls how much the average dominates relative to the minimum. This value is set by your project's **Reliability Scoring** preset (see below).

When all tests perform consistently well the two terms converge and the preset has no visible effect. The preset only matters when there is spread between the best and worst tests in a session — that is, when one or more tests are flaky or underperforming.

---

## Reliability Scoring Presets

The blend factor α is controlled by the **Reliability Scoring** setting, found in **Project Settings → Settings tab → Reliability Scoring**. Three named presets are available.

### Lenient (α = 0.8)

The weighted average dominates. A single underperforming test has relatively little influence on the session score.

**Best for:** projects with a large test suite where occasional flaky tests in non-critical paths are tolerable. Teams that want the session score to reflect the overall health of the suite rather than its weakest link.

**Example:** 50 tests at score 0.95, 1 test at score 0.20.

| Term | Value |
|------|-------|
| WeightedMean | ≈ 0.936 |
| Min | 0.20 |
| **Session score** | **0.80 × 0.936 + 0.20 × 0.20 = 0.789** |

---

### Balanced (α = 0.6) — *Default*

An equal-weighted blend that gives meaningful weight to both the average and the worst-case. This is the recommended starting point for most projects.

**Best for:** general-purpose projects where you want the session score to reflect typical reliability while still being sensitive to persistent outliers.

**Example (same suite as above):**

| Term | Value |
|------|-------|
| WeightedMean | ≈ 0.936 |
| Min | 0.20 |
| **Session score** | **0.60 × 0.936 + 0.40 × 0.20 = 0.642** |

---

### Strict (α = 0.3)

The minimum score dominates. A single unreliable test pulls the session score down significantly.

**Best for:** projects with zero-tolerance reliability requirements — such as safety-critical paths, payment flows, or any suite where one flaky test is considered a genuine signal that needs immediate attention.

**Example (same suite as above):**

| Term | Value |
|------|-------|
| WeightedMean | ≈ 0.936 |
| Min | 0.20 |
| **Session score** | **0.30 × 0.936 + 0.70 × 0.20 = 0.421** |

---

## Preset Comparison

The table below shows how the same test suite produces different session scores depending on the active preset:

| Preset | α | Session Score (example) | Sensitivity to worst test |
|--------|---|------------------------|--------------------------|
| Lenient | 0.8 | 0.789 | Low |
| Balanced | 0.6 | 0.642 | Medium |
| Strict | 0.3 | 0.421 | High |

---

## Important Behaviour Details

- **Default is Balanced.** Projects that have never changed this setting use α = 0.6 automatically — no action is required to get this behaviour.
- **The setting is per-project.** Different projects in the same workspace can use different presets independently.
- **Only affects session-level scores.** Individual test confidence scores and the project health distribution (Highly Reliable / Reliable / etc.) are not affected by this setting.
- **Takes effect on the next session.** Changing the preset does not retroactively recalculate historical session scores. The new value is applied from the next completed test session onwards.
- **When all tests score identically**, the preset has no effect — min equals mean and the blend is a no-op regardless of α.

---

## See Also

- [Understanding Confidence Scores](./understanding-confidence-scores.md) — How individual test confidence scores are calculated
- [Interpreting Test Results](./interpreting-test-results.md) — How to act on confidence scores and session results
- [Monitoring Test Health](../working-with-tests/monitoring-test-health.md) — Tracking trends over time
