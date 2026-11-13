using System.Net;
using System.Net.Sockets;

namespace OuttaContext.IntegrationTests.Support;

/// <summary>Serves a folder over HTTP on localhost, with Range support like a video CDN.</summary>
public sealed class StaticFileServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public StaticFileServer(string root)
    {
        Root = root;
        int port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _loop = Task.Run(ServeAsync);
    }

    public string Root { get; }

    public string BaseUrl { get; }

    public string UrlFor(string path) => $"{BaseUrl}/{Path.GetRelativePath(Root, path).Replace('\\', '/')}";

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _listener.Close();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _stop.Dispose();
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => Respond(context));
        }
    }

    private void Respond(HttpListenerContext context)
    {
        try
        {
            string path = Path.Combine(Root, Uri.UnescapeDataString(context.Request.Url!.AbsolutePath.TrimStart('/')));
            if (!File.Exists(path))
            {
                context.Response.StatusCode = 404;
                return;
            }

            byte[] body = File.ReadAllBytes(path);
            long first = 0;
            long last = body.Length - 1;
            if (context.Request.Headers["Range"] is { } range && range.StartsWith("bytes=", StringComparison.Ordinal))
            {
                string[] parts = range["bytes=".Length..].Split('-');
                first = parts[0].Length > 0 ? long.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) : 0;
                last = parts[1].Length > 0 ? Math.Min(long.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), last) : last;
                context.Response.StatusCode = 206;
                context.Response.Headers["Content-Range"] = $"bytes {first}-{last}/{body.Length}";
            }

            context.Response.Headers["Accept-Ranges"] = "bytes";
            context.Response.ContentLength64 = last - first + 1;
            context.Response.OutputStream.Write(body, (int)first, (int)(last - first + 1));
        }
        catch (Exception exception) when (exception is HttpListenerException or IOException)
        {
            // ffmpeg and yt-dlp hang up mid-transfer on purpose.
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
