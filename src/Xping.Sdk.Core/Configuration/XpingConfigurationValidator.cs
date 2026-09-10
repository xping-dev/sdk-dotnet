/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Options;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Configuration;

/// <summary>
/// Runs <see cref="XpingConfiguration.Validate"/> as part of options validation, reporting each
/// problem it found.
/// </summary>
/// <remarks>
/// A predicate registered through <c>OptionsBuilder.Validate</c> can only answer yes or no, so the
/// failures reach the caller as "A validation error has occurred." That is thin for configuration
/// the caller wrote, and thinner still for the case this exists to report: a value an environment
/// variable put there, where the reader has to guess which of a dozen <c>XPING_*</c> settings the
/// pipeline got wrong. Naming them costs one type.
/// </remarks>
internal sealed class XpingConfigurationValidator : IValidateOptions<XpingConfiguration>
{
    public ValidateOptionsResult Validate(string? name, XpingConfiguration options)
    {
        IReadOnlyList<string> errors = options.RequireNotNull().Validate();

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
