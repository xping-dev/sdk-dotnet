/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth.Store;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Tests.Auth.Store;

/// <summary>
/// Records and the production serializer for the credential store tests.
/// </summary>
/// <remarks>
/// The Cloud URLs are under <c>.invalid</c> so no test record could ever be mistaken for, or
/// replace, a real sign-in (cli-auth-cli-spec §17.2).
/// </remarks>
internal static class CredentialTestData
{
    public const string CloudUrl = "https://tests.invalid";

    public const string OtherCloudUrl = "https://other.tests.invalid";

    public const string RefreshToken = "refresh-token-0123456789";

    public const string AccessToken = "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJ0ZXN0In0.c2lnbmF0dXJl";

    public static IXpingSerializer Serializer { get; } =
        new ServiceCollection().AddXpingSerialization().BuildServiceProvider().GetRequiredService<IXpingSerializer>();

    public static CredentialRecord Record(string cloudUrl = CloudUrl, string refreshToken = RefreshToken) =>
        new(
            CredentialRecord.CurrentSchemaVersion,
            cloudUrl,
            refreshToken,
            AccessToken,
            new DateTimeOffset(2026, 9, 29, 10, 15, 0, TimeSpan.Zero),
            "01J8WORKSPACE",
            "01J8SESSION",
            "01J8USER",
            "jane@example.com",
            "https://api.tests.invalid",
            new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
}
