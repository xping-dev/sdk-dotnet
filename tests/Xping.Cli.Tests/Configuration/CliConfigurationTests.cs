/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Configuration;

namespace Xping.Cli.Tests.Configuration;

public sealed class CliConfigurationTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "xping-cli-configuration-tests", Guid.NewGuid().ToString("N"));

    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);

    public CliConfigurationTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private CliConfiguration Load(string? cloudUrlFlag = null, string? apiKeyFlag = null) =>
        CliConfiguration.Load(
            cloudUrlFlag, apiKeyFlag, _directory, name => _environment.GetValueOrDefault(name));

    private void WriteSettings(string fileName, string json) =>
        File.WriteAllText(Path.Combine(_directory, fileName), json);

    [Fact]
    public void NothingConfiguredGivesTheDefaultCloudAndNoKey()
    {
        CliConfiguration configuration = Load();

        Assert.Equal(new ConfiguredValue(CloudUrl.Default, ConfigurationSource.Default, "default"), configuration.CloudUrl);
        Assert.Null(configuration.ApiKey);
        Assert.Null(configuration.ProjectId);
    }

    [Fact]
    public void TheFlagWinsOverEverySource()
    {
        _environment["XPING_CLOUDURL"] = "https://env.example";
        _environment["XPING_APIKEY"] = "env-key";
        WriteSettings("appsettings.json", """{ "Xping": { "CloudUrl": "https://file.example", "ApiKey": "file-key" } }""");

        CliConfiguration configuration = Load("https://flag.example", "flag-key");

        Assert.Equal(new ConfiguredValue("https://flag.example", ConfigurationSource.Flag, "--cloud-url"), configuration.CloudUrl);
        Assert.Equal(new ConfiguredValue("flag-key", ConfigurationSource.Flag, "--api-key"), configuration.ApiKey);
    }

    [Fact]
    public void ThePrefixedVariableWinsOverTheNestedOneAndTheFiles()
    {
        _environment["XPING_APIKEY"] = "prefixed";
        _environment["Xping__ApiKey"] = "nested";
        WriteSettings("appsettings.json", """{ "Xping": { "ApiKey": "file" } }""");

        Assert.Equal(new ConfiguredValue("prefixed", ConfigurationSource.Environment, "XPING_APIKEY"), Load().ApiKey);
    }

    [Fact]
    public void TheNestedVariableWinsOverTheFiles()
    {
        _environment["Xping__ProjectId"] = "nested";
        WriteSettings("appsettings.json", """{ "Xping": { "ProjectId": "file" } }""");

        Assert.Equal(new ConfiguredValue("nested", ConfigurationSource.Environment, "Xping__ProjectId"), Load().ProjectId);
    }

    [Fact]
    public void TheEnvironmentSpecificFileWinsOverTheBaseFileAndIsNamed()
    {
        _environment["DOTNET_ENVIRONMENT"] = "Development";
        WriteSettings("appsettings.json", """{ "Xping": { "ApiKey": "base" } }""");
        WriteSettings("appsettings.Development.json", """{ "Xping": { "ApiKey": "development" } }""");

        Assert.Equal(
            new ConfiguredValue("development", ConfigurationSource.AppSettings, "Xping:ApiKey in appsettings.Development.json"),
            Load().ApiKey);
    }

    [Fact]
    public void AspNetCoreEnvironmentIsPreferredToDotnetEnvironment()
    {
        _environment["ASPNETCORE_ENVIRONMENT"] = "Staging";
        _environment["DOTNET_ENVIRONMENT"] = "Development";
        WriteSettings("appsettings.Staging.json", """{ "Xping": { "ApiKey": "staging" } }""");
        WriteSettings("appsettings.Development.json", """{ "Xping": { "ApiKey": "development" } }""");

        Assert.Equal("staging", Load().ApiKey?.Value);
    }

    [Fact]
    public void ABlankValueCountsAsUnset()
    {
        _environment["XPING_APIKEY"] = "   ";
        WriteSettings("appsettings.json", """{ "Xping": { "ApiKey": "file" } }""");

        Assert.Equal(ConfigurationSource.AppSettings, Load().ApiKey?.Source);
    }

    [Fact]
    public void ACloudUrlFromTheEnvironmentIsNormalized()
    {
        _environment["XPING_CLOUDURL"] = "HTTPS://App.Xping.io/";

        Assert.Equal("https://app.xping.io", Load().CloudUrl.Value);
    }

    [Fact]
    public void AnInvalidCloudUrlNamesTheVariableThatHoldsIt()
    {
        _environment["Xping__CloudUrl"] = "http://example.com";

        var ex = Assert.Throws<CliConfigurationException>(() => Load());
        Assert.StartsWith("Xping__CloudUrl must use https", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInvalidCloudUrlNamesTheFileThatHoldsIt()
    {
        WriteSettings("appsettings.json", """{ "Xping": { "CloudUrl": "https://app.xping.io/portal" } }""");

        var ex = Assert.Throws<CliConfigurationException>(() => Load());
        Assert.StartsWith("Xping:CloudUrl in appsettings.json must not contain a path", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMalformedSettingsFileIsAConfigurationError()
    {
        WriteSettings("appsettings.json", "{ not json");

        Assert.Throws<CliConfigurationException>(() => Load());
    }
}
