/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// How improbable a standard normal deviate is, out to the far tail.
/// </summary>
/// <remarks>
/// <para>
/// One function, for one caller: <see cref="CochranArmitage"/> refers its trend statistic to the
/// normal distribution and needs the two-sided tail beyond it. Everything else in
/// <c>Report/Scoring</c> is exact or permutation-based and needs no normal at all.
/// </para>
/// <para>
/// <b>Computed as a complement, never as one minus a cumulative.</b> The obvious spelling,
/// <c>2 × (1 − Φ(z))</c>, has already lost half of its significant digits by <c>z = 6</c> and returns
/// literally zero from about <c>z = 8.3</c>, because <c>Φ(8.3)</c> rounds to 1.0 in a double. A
/// report that publishes a probability of zero is claiming certainty, which no window a store holds
/// earns — the same promise <see cref="FisherExact"/> makes about its own arithmetic. The tail is
/// therefore evaluated directly as <c>erfc(|z| / √2)</c>, which keeps its <i>relative</i> accuracy all
/// the way down to the smallest positive double.
/// </para>
/// <para>
/// <b>Cody's rational approximations, in three branches.</b> W. J. Cody's minimax rationals (the
/// <c>CALERF</c> of ALGORITHM 715 / SPECFUN) hold a relative error near machine epsilon across the
/// whole line, by never letting a subtraction cancel: erf is evaluated near the origin, erfc directly
/// in the middle range, and an asymptotic form scaled by <c>exp(−x²)/x</c> in the far tail. The
/// exponential is split as <c>exp(−ŷ²) × exp(−(y − ŷ)(y + ŷ))</c> with <c>ŷ</c> truncated to a
/// sixteenth, so the large factor is computed from an exactly representable argument and only the
/// small correction carries rounding.
/// </para>
/// <para>
/// <b>What was rejected.</b> Abramowitz and Stegun 7.1.26 is five lines and has an <i>absolute</i>
/// error of 1.5e-7, so every tail probability this is actually asked for would be noise. Hart's and
/// Zelen–Severo's rationals fail the same way. Taking a dependency on MathNet.Numerics for one
/// function would put a numerics package inside a packaged <c>dotnet tool</c>.
/// </para>
/// </remarks>
internal static class NormalTail
{
    /// <summary>1/√π, the leading coefficient of the asymptotic expansion.</summary>
    private const double InverseRootPi = 5.6418958354775628695e-1;

    /// <summary>Where evaluating erf stops being better conditioned than evaluating erfc.</summary>
    private const double ErfThreshold = 0.46875;

    /// <summary>Where the rational in <c>x</c> gives way to the asymptotic form in <c>1/x²</c>.</summary>
    private const double AsymptoticThreshold = 4.0;

    private static readonly double[] ErfNumerator =
    [
        3.16112374387056560e00, 1.13864154151050156e02, 3.77485237685302021e02,
        3.20937758913846947e03, 1.85777706184603153e-1
    ];

    private static readonly double[] ErfDenominator =
    [
        2.36012909523441209e01, 2.44024637934444173e02, 1.28261652607737228e03,
        2.84423683343917062e03
    ];

    private static readonly double[] MiddleNumerator =
    [
        5.64188496988670089e-1, 8.88314979438837594e00, 6.61191906371416295e01,
        2.98635138197400131e02, 8.81952221241769090e02, 1.71204761263407058e03,
        2.05107837782607147e03, 1.23033935479799725e03, 2.15311535474403846e-8
    ];

    private static readonly double[] MiddleDenominator =
    [
        1.57449261107098347e01, 1.17693950891312499e02, 5.37181101862009858e02,
        1.62138957456669019e03, 3.29079923573345963e03, 4.36261909014324716e03,
        3.43936767414372164e03, 1.23033935480374942e03
    ];

    private static readonly double[] TailNumerator =
    [
        3.05326634961232344e-1, 3.60344899949804439e-1, 1.25781726111229246e-1,
        1.60837851487422766e-2, 6.58749161529837803e-4, 1.63153871373020978e-2
    ];

    private static readonly double[] TailDenominator =
    [
        2.56852019228982242e00, 1.87295284992346047e00, 5.27905102951428412e-1,
        6.05183413124413191e-2, 2.33520497626869185e-3
    ];

    /// <summary>
    /// Probability that a standard normal deviate is at least as far from zero as
    /// <paramref name="z"/>.
    /// </summary>
    /// <param name="z">The deviate; its sign is irrelevant.</param>
    /// <returns>
    /// <c>P(|Z| ≥ |z|)</c>, in [0,1]. 1 at <c>z = 0</c> and 1 for any value that is not a finite
    /// number, which is the claim-nothing answer a degenerate statistic deserves. It reaches exactly
    /// zero only past <c>|z| ≈ 38</c>, where the true probability is below the smallest positive
    /// double — three times further out than a naive complement gets, and further than any window a
    /// store holds can reach.
    /// </returns>
    /// <remarks>
    /// Two-sided, because every caller discovers the direction of its effect from the data rather
    /// than pre-registering it, and a one-sided probability taken after looking at the sign is half
    /// the probability the comparison earned.
    /// </remarks>
    public static double TwoSidedPValue(double z)
    {
        if (!double.IsFinite(z))
            return 1.0;

        return Math.Clamp(Erfc(Math.Abs(z) / Math.Sqrt(2.0)), 0.0, 1.0);
    }

    /// <summary>
    /// Complementary error function for a non-negative argument.
    /// </summary>
    /// <param name="x">The argument, which callers have already made non-negative.</param>
    /// <returns><c>erfc(x)</c>.</returns>
    private static double Erfc(double x)
    {
        if (x <= ErfThreshold)
            return 1.0 - Erf(x);

        return x <= AsymptoticThreshold ? Middle(x) : Tail(x);
    }

    /// <summary>
    /// Error function on the interval where evaluating the complement would cancel.
    /// </summary>
    private static double Erf(double x)
    {
        double square = x * x;
        double numerator = ErfNumerator[4] * square;
        double denominator = square;

        for (int i = 0; i < 3; i++)
        {
            numerator = (numerator + ErfNumerator[i]) * square;
            denominator = (denominator + ErfDenominator[i]) * square;
        }

        return x * (numerator + ErfNumerator[3]) / (denominator + ErfDenominator[3]);
    }

    /// <summary>
    /// Complementary error function on the middle range, as a rational in <paramref name="x"/>.
    /// </summary>
    private static double Middle(double x)
    {
        double numerator = MiddleNumerator[8] * x;
        double denominator = x;

        for (int i = 0; i < 7; i++)
        {
            numerator = (numerator + MiddleNumerator[i]) * x;
            denominator = (denominator + MiddleDenominator[i]) * x;
        }

        return Scale(x, (numerator + MiddleNumerator[7]) / (denominator + MiddleDenominator[7]));
    }

    /// <summary>
    /// Complementary error function in the far tail, as a rational in <c>1/x²</c>.
    /// </summary>
    private static double Tail(double x)
    {
        double inverse = 1.0 / (x * x);
        double numerator = TailNumerator[5] * inverse;
        double denominator = inverse;

        for (int i = 0; i < 4; i++)
        {
            numerator = (numerator + TailNumerator[i]) * inverse;
            denominator = (denominator + TailDenominator[i]) * inverse;
        }

        double series = inverse * (numerator + TailNumerator[4]) / (denominator + TailDenominator[4]);

        return Scale(x, (InverseRootPi - series) / x);
    }

    /// <summary>
    /// Multiplies a scaled complementary error function by <c>exp(−x²)</c>.
    /// </summary>
    /// <param name="x">The argument.</param>
    /// <param name="scaled">The value of <c>exp(x²) erfc(x)</c>.</param>
    /// <returns><c>erfc(x)</c>.</returns>
    /// <remarks>
    /// Split at a sixteenth so that the large factor's argument is exactly representable and the
    /// rounding error in <c>x²</c> — which is multiplied by the answer itself — lands only in the
    /// small correction factor. Evaluating <c>Math.Exp(-x * x)</c> directly loses about a digit for
    /// every power of ten the answer falls.
    /// </remarks>
    private static double Scale(double x, double scaled)
    {
        double truncated = Math.Floor(x * 16.0) / 16.0;
        double remainder = (x - truncated) * (x + truncated);

        return Math.Exp(-truncated * truncated) * Math.Exp(-remainder) * scaled;
    }
}
