/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Xping.Sdk.Core.Configuration;

/// <summary>
/// Resolves CaptureStackTraces from options or raw configuration in failure paths.
/// </summary>
public static class CaptureStackTraceConfigurationResolver
{
    /// <summary>
    /// Resolves the effective CaptureStackTraces value without propagating options validation failures.
    /// </summary>
    /// <param name="services">The service provider used to resolve options/configuration.</param>
    /// <returns>The effective CaptureStackTraces value.</returns>
    public static bool ResolveCaptureStackTraces(IServiceProvider services)
    {
        try
        {
            return services.GetRequiredService<IOptions<XpingConfiguration>>().Value.CaptureStackTraces;
        }
        catch (OptionsValidationException)
        {
            IConfiguration? configuration = services.GetService<IConfiguration>();
            bool? configuredValue = configuration?.GetSection("Xping").GetValue<bool?>("CaptureStackTraces");
            if (configuredValue.HasValue)
            {
                return configuredValue.Value;
            }

            string? legacyEnvValue = Environment.GetEnvironmentVariable("XPING_CAPTURESTACKTRACES");
            if (bool.TryParse(legacyEnvValue, out bool parsedLegacyValue))
            {
                return parsedLegacyValue;
            }

            return true;
        }
    }
}
