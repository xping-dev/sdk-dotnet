/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Integration.Tests.Infrastructure;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Simple HTTP server for testing API interactions without external dependencies.
/// </summary>
internal sealed class MockApiServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly Task _listenerTask;
    private readonly List<ReceivedRequest> _receivedRequests;
    private readonly object _lock = new();

    public string BaseUrl { get; }
    public MockApiServerBehavior Behavior { get; set; }
    public IReadOnlyList<ReceivedRequest> ReceivedRequests
    {
        get
        {
            lock (_lock)
            {
                return _receivedRequests.ToList();
            }
        }
    }

    public MockApiServer(int port = 0)
    {
        _receivedRequests = [];
        _cts = new CancellationTokenSource();
        Behavior = new MockApiServerBehavior();

        // Find available port if 0 is specified
        if (port == 0)
        {
            port = FindAvailablePort();
        }

        BaseUrl = $"http://localhost:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(BaseUrl);

        try
        {
            _listener.Start();
            _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
        }
        catch
        {
            _listener.Close();
            throw;
        }
    }

    private static int FindAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Continue listening
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Read request body
            string? requestBody = null;
            if (request.HasEntityBody)
            {
                using var stream = request.ContentEncoding?.WebName == "gzip" ||
                    request.Headers["Content-Encoding"]?.Equals("gzip", StringComparison.OrdinalIgnoreCase) == true
                    ? new GZipStream(request.InputStream, CompressionMode.Decompress)
                    : request.InputStream;
                using var reader = new StreamReader(stream, request.ContentEncoding ?? Encoding.UTF8);
                requestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            // Store received request
            var receivedRequest = new ReceivedRequest
            {
                Method = request.HttpMethod,
                Url = request.Url?.ToString() ?? string.Empty,
                Headers = request.Headers.AllKeys.ToDictionary(k => k ?? "", k => request.Headers[k] ?? ""),
                Body = requestBody,
                ReceivedAt = DateTime.UtcNow
            };

            lock (_lock)
            {
                _receivedRequests.Add(receivedRequest);
            }

            // Apply behavior
            await ApplyBehaviorAsync(response, receivedRequest).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            var errorBytes = Encoding.UTF8.GetBytes(ex.Message);
            await response.OutputStream.WriteAsync(errorBytes).ConfigureAwait(false);
        }
        finally
        {
            response.Close();
        }
    }

    private async Task ApplyBehaviorAsync(HttpListenerResponse response, ReceivedRequest request)
    {
        // Check for delay
        if (Behavior.DelayMs > 0)
        {
            await Task.Delay(Behavior.DelayMs).ConfigureAwait(false);
        }

        // Check for failure rate
        if (Behavior.FailureRate > 0)
        {
#pragma warning disable CA5394 // Do not use insecure randomness - this is for testing only
            var random = new Random();
            if (random.NextDouble() < Behavior.FailureRate)
#pragma warning restore CA5394
            {
                response.StatusCode = Behavior.FailureStatusCode;
                var errorResponse = new { error = "Simulated failure" };
                await WriteJsonResponseAsync(response, errorResponse).ConfigureAwait(false);
                return;
            }
        }

        // Check for custom response
        if (Behavior.CustomResponse != null)
        {
            response.StatusCode = Behavior.CustomStatusCode;
            await WriteJsonResponseAsync(response, Behavior.CustomResponse).ConfigureAwait(false);
            return;
        }

        // Default success response
        response.StatusCode = 201;
        var successResponse = new
        {
            executionCount = 1,
            receiptId = Guid.NewGuid().ToString()
        };
        await WriteJsonResponseAsync(response, successResponse).ConfigureAwait(false);
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse response, object data)
    {
        response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    public void ClearRequests()
    {
        lock (_lock)
        {
            _receivedRequests.Clear();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();

        try
        {
            _listenerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore
        }

        _cts.Dispose();
    }
}
