using System.Diagnostics;
using System.Net;

namespace Xping.Sdk.IntegrationTests.HttpServer;

internal static class InMemoryHttpServer
{
    public static Task TestServer(
        Action<HttpListenerResponse> responseBuilder,
        CancellationToken cancellationToken = default)
    {
        return TestServer(responseBuilder, requestReceived: (request) => { }, cancellationToken);
    }

    public static Task TestServer(
        Action<HttpListenerResponse> responseBuilder,
        Action<HttpListenerRequest> requestReceived,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                using HttpListener listener = new();
                listener.Prefixes.Add(GetTestServerAddress().AbsoluteUri);
                listener.Start();
                // GetContext method blocks while waiting for a request. 
                HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                // Examine client request received
                requestReceived?.Invoke(context.Request);
                // Build HttpListenerResponse
                responseBuilder(context.Response);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Debugger.Break();
            }
        }, cancellationToken);
    }

    public static Task TestServer(
        Action<HttpListenerResponse> responseBuilder,
        Action<HttpListenerRequest> requestReceived,
        Uri[] uriPrefixes,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                using HttpListener listener = new();
                uriPrefixes.ToList().ForEach(uriPrefix => listener.Prefixes.Add(uriPrefix.AbsoluteUri));
                listener.Start();

                // The TestServer is configured to handle all registered uriPrefixes, ensuring that it can serve
                // requests to any of them during our Tests.
                for (int i = 0; i < uriPrefixes.Length; i++)
                {
                    // GetContext method blocks while waiting for a request. 
                    HttpListenerContext context = await listener.GetContextAsync();
                    // Examine client request received
                    requestReceived?.Invoke(context.Request);
                    // Build HttpListenerResponse
                    responseBuilder(context.Response);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Debugger.Break();
            }
        }, cancellationToken);
    }

    public static Uri GetTestServerAddress()
    {
        return new Uri($"http://localhost:8080/");
    }
}
