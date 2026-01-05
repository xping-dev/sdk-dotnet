/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.NUnit;

// NOTE: This project tests Xping.Sdk.XUnit components but uses NUnit as its test framework.
// This prevents XpingContext.Reset() calls in tests from interfering with dogfooding,
// since dogfooding uses Xping.Sdk.NUnit.XpingContext (a separate static class).

// Apply XpingTrack to all tests in this assembly for dogfooding
[assembly: XpingTrack]
