/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Xping.Cli.Auth.Loopback;

/// <summary>
/// What the browser brought back to the listener.
/// </summary>
internal abstract record CallbackResult
{
    private CallbackResult()
    {
    }

    /// <summary>
    /// The user consented; <paramref name="Value"/> is the authorization code.
    /// </summary>
    internal sealed record Code(string Value) : CallbackResult
    {
        // The generated ToString would print the code.
        public override string ToString() => "Code { Value = [redacted] }";
    }

    /// <summary>
    /// The server redirected with an OAuth error, such as <c>access_denied</c>.
    /// </summary>
    internal sealed record Error(string Value, string? Description) : CallbackResult;
}

/// <summary>
/// The one-shot HTTP listener on a loopback port that the browser is redirected to
/// (cli-auth-cli-spec §4.3–§4.5, §4.9).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HttpListener"/> cannot bind port 0, so a free port is probed with a
/// <see cref="TcpListener"/> first. Another process can take it in between; that is why
/// <c>127.0.0.1</c> is tried three times before <c>[::1]</c> (contract §3.1, §10.2). The prefix is
/// never <c>localhost</c> or a wildcard: only this machine's loopback may reach it.
/// </para>
/// <para>
/// Requests that are not the genuine callback are answered and ignored (A-6): a stray request or a
/// redirect with the wrong <c>state</c> must not end a sign-in the user is still completing.
/// </para>
/// </remarks>
internal sealed class LoopbackListener : IAsyncDisposable
{
    /// <summary>The path the browser is redirected to.</summary>
    public const string CallbackPath = "/callback";

    private const int Ipv4Attempts = 3;

    private readonly HttpListener _listener;
    private readonly ILogger _logger;
    private int _stopped;

    private LoopbackListener(HttpListener listener, string redirectUri, ILogger logger)
    {
        _listener = listener;
        _logger = logger;
        RedirectUri = redirectUri;
    }

    /// <summary>
    /// Gets the exact <c>redirect_uri</c> of this listener, such as
    /// <c>http://127.0.0.1:53124/callback</c>.
    /// </summary>
    public string RedirectUri { get; }

    /// <summary>
    /// Binds a listener on a free loopback port.
    /// </summary>
    /// <param name="logger">Receives the bind attempts, for <c>--verbose</c>.</param>
    /// <param name="probePort">
    /// Finds a free port on an address; tests replace it to hand out a port they hold.
    /// </param>
    /// <exception cref="AuthFailureException">No loopback port could be opened.</exception>
    public static LoopbackListener Start(ILogger logger, Func<IPAddress, int>? probePort = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        probePort ??= ProbePort;

        for (int attempt = 0; attempt <= Ipv4Attempts; attempt++)
        {
            // Three tries on 127.0.0.1, then one on [::1].
            LoopbackListener? bound = attempt < Ipv4Attempts
                ? TryBind(IPAddress.Loopback, "127.0.0.1", probePort, logger)
                : TryBind(IPAddress.IPv6Loopback, "[::1]", probePort, logger);

            if (bound is not null)
                return bound;
        }

        throw new AuthFailureException(
            AuthExitCodes.LoginFailed,
            AuthErrorCodes.OAuthError,
            "Could not open a local port for the browser to return to. Run `xping login --device` instead.");
    }

    /// <summary>
    /// Waits for the genuine callback of <paramref name="pkce"/>'s sign-in and answers the browser.
    /// </summary>
    /// <remarks>
    /// The answer to the genuine callback is sent and closed before this returns, so the browser
    /// shows its page while the CLI talks to the Portal.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> fired.</exception>
    /// <exception cref="AuthFailureException">The listener stopped for another reason.</exception>
    public async Task<CallbackResult> WaitForCallbackAsync(Pkce pkce, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pkce);

        // GetContextAsync takes no token; stopping the listener is the only way to end the wait.
        using CancellationTokenRegistration registration = cancellationToken.Register(Stop);

        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new AuthFailureException(
                    AuthExitCodes.LoginFailed,
                    AuthErrorCodes.OAuthError,
                    "The local port the browser returns to closed unexpectedly. Run `xping login` again.",
                    ex);
            }

            if (await AnswerAsync(context, pkce).ConfigureAwait(false) is { } result)
                return result;
        }
    }

    /// <summary>
    /// Stops listening. Safe to call more than once and from any thread.
    /// </summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
            return;

        try
        {
            _listener.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Already closed.
        }

        _listener.Close();
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }

    private async Task<CallbackResult?> AnswerAsync(HttpListenerContext context, Pkce pkce)
    {
        HttpListenerRequest request = context.Request;

        if (!string.Equals(request.HttpMethod, "GET", StringComparison.Ordinal)
            || !string.Equals(request.Url?.AbsolutePath, CallbackPath, StringComparison.Ordinal))
        {
            await RespondAsync(context.Response, 404, body: null).ConfigureAwait(false);
            return null;
        }

        // State first, before anything else in the query is looked at (contract §3.1 step 8).
        if (!pkce.StateMatches(request.QueryString["state"]))
        {
            _logger.LogInformation("Callback with wrong state ignored");
            await RespondAsync(context.Response, 400, LoopbackPages.Error(error: null)).ConfigureAwait(false);
            return null;
        }

        if (request.QueryString["error"] is { Length: > 0 } error)
        {
            await RespondAsync(context.Response, 200, LoopbackPages.Error(error)).ConfigureAwait(false);
            return new CallbackResult.Error(error, request.QueryString["error_description"]);
        }

        if (request.QueryString["code"] is { Length: > 0 } code)
        {
            Redaction.AddSecret(code);
            await RespondAsync(context.Response, 200, LoopbackPages.Success).ConfigureAwait(false);
            return new CallbackResult.Code(code);
        }

        await RespondAsync(context.Response, 400, LoopbackPages.Error(error: null)).ConfigureAwait(false);
        return null;
    }

    private static async Task RespondAsync(HttpListenerResponse response, int status, string? body)
    {
        try
        {
            response.StatusCode = status;
            response.KeepAlive = false;
            response.ContentType = "text/html; charset=utf-8";
            response.Headers["Cache-Control"] = "no-store";
            response.Headers["Referrer-Policy"] = "no-referrer";
            response.Headers["X-Content-Type-Options"] = "nosniff";

            byte[] bytes = body is null ? [] : Encoding.UTF8.GetBytes(body);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            response.Close();
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
        {
            // The browser went away before the answer; the sign-in itself is unaffected.
            response.Abort();
        }
    }

    private static LoopbackListener? TryBind(IPAddress address, string host, Func<IPAddress, int> probePort, ILogger logger)
    {
        int port;
        try
        {
            port = probePort(address);
        }
        catch (SocketException ex)
        {
            // An address family the machine does not have, typically IPv6 switched off.
            logger.LogInformation("Could not probe a port on {Host}: {Reason}", host, ex.SocketErrorCode);
            return null;
        }

        string prefix = string.Create(CultureInfo.InvariantCulture, $"http://{host}:{port}/");
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            logger.LogInformation("Could not listen on {Host}:{Port}: {Reason}", host, port, ex.ErrorCode);
            listener.Close();
            return null;
        }

        return new LoopbackListener(
            listener,
            string.Create(CultureInfo.InvariantCulture, $"http://{host}:{port}{CallbackPath}"),
            logger);
    }

    private static int ProbePort(IPAddress address)
    {
        using var probe = new TcpListener(address, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
