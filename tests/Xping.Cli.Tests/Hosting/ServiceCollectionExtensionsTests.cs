/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Xping.Cli.Hosting;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Services.Upload;

namespace Xping.Cli.Tests.Hosting;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void TheCliAddsNothingToTheSdkUploaderClient()
    {
        HttpClientFactoryOptions alone = UploaderOptions(new ServiceCollection().AddXpingUploader());
        HttpClientFactoryOptions withCli = UploaderOptions(
            new ServiceCollection()
                .AddXpingCliServices(TextWriter.Null, TextWriter.Null, TextReader.Null, Terminals.All(false))
                .AddXpingUploader());

        Assert.Equal(alone.HttpMessageHandlerBuilderActions.Count, withCli.HttpMessageHandlerBuilderActions.Count);
        Assert.Equal(alone.HttpClientActions.Count, withCli.HttpClientActions.Count);
    }

    private static HttpClientFactoryOptions UploaderOptions(IServiceCollection services)
    {
        using ServiceProvider provider = services.AddLogging().BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(nameof(IXpingUploader));
    }
}
