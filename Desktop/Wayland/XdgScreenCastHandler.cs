using Desktop.DBus;
using Tmds.DBus.Protocol;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Desktop.Wayland;

internal static class XdgScreenCastHandler
{
    public static async Task<XdgScreenData?> PromptUserAsync(WaylandOutput output)
    {
        var data = new XdgScreenData(output);
        if (await data.InitDbusAsync())
            return data;
        return null;
    }
}

internal class XdgScreenData : PipewireOutput
{
    private Connection _dbus = null!;
    private DesktopService _service = null!;
    private ScreenCast _screenCast = null!;

    private readonly string _token;
    private string? _requestPath;
    private string? _sessionPath;

    public XdgScreenData(WaylandOutput output)
    {
        Name = output.Name;
        Handle = output.Handle;
        IdName = output.IdName;
        Position = output.Position;
        Size = output.Size;
        _token = $"xdg_screen_{output.IdName}";
    }

    public async Task<bool> InitDbusAsync()
    {
        _dbus = new Connection(Address.Session!);
        await _dbus.ConnectAsync();

        var myName = _dbus.UniqueName![1..].Replace(".", "_");
        _requestPath = $"/org/freedesktop/portal/desktop/request/{myName}/{_token}";

        _service = new DesktopService(_dbus, "org.freedesktop.portal.Desktop");
        _screenCast = _service.CreateScreenCast("/org/freedesktop/portal/desktop");

        if (await CreateSessionAsync() && await SelectSourcesAsync() && await StartCaptureAsync())
        {
            return true;
        }

        Dispose();
        return false;
    }

    private async Task<bool> CreateSessionAsync()
    {
        var options = new Dictionary<string, object>
        {
            ["handle_token"] = _token,
            ["session_handle_token"] = _token,
        };

        var state = 0L;
        var watcher = await _screenCast.WatchSignalAsync("org.freedesktop.portal.Desktop",
            "org.freedesktop.portal.Request", _requestPath!, "Response",
            (m, _) =>
            {
                var r = m.GetBodyReader();
                return new ScreenCastResponse
                {
                    Response = r.ReadUInt32(),
                    Results = r.ReadDictionary<string, object>()
                };
            },
            (e, t) =>
            {
                if (Interlocked.CompareExchange(ref state, -1, 0) != 0)
                    return;

                if (e != null)
                {
                    Console.WriteLine($"ERR Could not create ScreenCast session: {e.Message}");
                    Interlocked.Exchange(ref state, 2);
                    return;
                }

                if (t.Response != 0)
                {
                    if (t.Response != 1)
                        Console.WriteLine($"ERR Could not create ScreenCast session: {t.Response}");
                    Interlocked.Exchange(ref state, 2);
                    return;
                }

                _sessionPath = t.Results["session_handle"] as string ??
                               throw new Exception("Invalid session_handle");
                Interlocked.Exchange(ref state, 1);
            },
            false);

        await _screenCast.CreateSessionAsync(options);

        long val;
        while ((val = Interlocked.Read(ref state)) <= 0)
            await Task.Delay(100);
        watcher.Dispose();
        return val == 1;
    }

    private async Task<bool> SelectSourcesAsync()
    {
        var options = new Dictionary<string, object>
        {
            ["handle_token"] = _token,
            ["type"] = 1U,
            ["cursor_mode"] = 2U, // embedded
            ["persist_mode"] = 2U, // persistent
        };

        if (Config.TryGetFile($"screen-{Name}.token", out var file))
            options.Add("restore_token", (await File.ReadAllLinesAsync(file))[0]);

        var state = 0L;
        var watcher = await _screenCast.WatchSignalAsync("org.freedesktop.portal.Desktop",
            "org.freedesktop.portal.Request", _requestPath!, "Response",
            (m, _) =>
            {
                var r = m.GetBodyReader();
                return new ScreenCastResponse
                {
                    Response = r.ReadUInt32(),
                    Results = r.ReadDictionary<string, object>()
                };
            },
            (e, t) =>
            {
                if (Interlocked.CompareExchange(ref state, -1, 0) != 0)
                    return;

                if (e != null)
                {
                    Console.WriteLine($"ERR Could not create ScreenCast session: {e.Message}");
                    Interlocked.Exchange(ref state, 2);
                    return;
                }
                if (t.Response != 0)
                {
                    if (t.Response != 1)
                        Console.WriteLine($"ERR Could not select ScreenCast source: {t.Response}");
                    Interlocked.Exchange(ref state, 2);
                    return;
                }

                Interlocked.Exchange(ref state, 1);
            },
            false);

        await _screenCast.SelectSourcesAsync(_sessionPath!, options);

        long val;
        while ((val = Interlocked.Read(ref state)) <= 0)
            await Task.Delay(100);
        watcher.Dispose();
        return val == 1;
    }

    private async Task<bool> StartCaptureAsync()
    {
        var options = new Dictionary<string, object>
        {
            ["handle_token"] = _token,
        };

        var state = 0L;
        var watcher = await _screenCast.WatchSignalAsync("org.freedesktop.portal.Desktop",
            "org.freedesktop.portal.Request", _requestPath!, "Response",
            (m, _) =>
            {
                var r = m.GetBodyReader();
                var response = new ScreenCastResponse
                {
                    Response = r.ReadUInt32(),
                    Results = r.ReadDictionary<string, object>()
                };

                return response;
            },
            (e, t) =>
            {
                if (Interlocked.CompareExchange(ref state, -1, 0) != 0)
                    return;

                if (e != null)
                {
                    Console.WriteLine($"ERR Could not Start ScreenCast session: {e.Message}");
                    Interlocked.Exchange(ref state, 2);
                    return;
                }
                if (t.Response != 0)
                {
                    if (t.Response != 1)
                        Console.WriteLine($"ERR Could not Start ScreenCast source: {t.Response}");
                    Interlocked.Exchange(ref state, 2);
                    return;
                }

                if (!t.Results.TryGetValue("streams", out var maybeStreams)
                    || !(maybeStreams is ValueTuple<uint, Dictionary<string, object>>[] streams))
                {
                    Console.WriteLine($"ERR Could not Start ScreenCast source: Unexpected response");
                    Interlocked.Exchange(ref state, 2);
                    return;
                }

                if (t.Results.TryGetValue("restore_token", out var maybeRestoreToken)
                    && maybeRestoreToken is string restoreToken)
                {
                    if (!Directory.Exists(Config.UserConfigFolder))
                        Directory.CreateDirectory(Config.UserConfigFolder);

                    var path = Path.Combine(Config.UserConfigFolder, $"screen-{Name}.token");
                    File.WriteAllText(path, restoreToken);
                }

                if (streams[0].Item2.TryGetValue("size", out var maybeSize)
                    && maybeSize is ValueTuple<int, int> size)
                    Size = new Vector2Int(size.Item1, size.Item2);
                else
                {
                    Console.WriteLine($"ERR Could not Start ScreenCast source: Unexpected format: size");
                    Interlocked.Exchange(ref state, 2);
                    return;
                }

                NodeId = streams[0].Item1;
                Interlocked.Exchange(ref state, 1);
            },
            false);

        await _screenCast.StartAsync(_sessionPath!, "", options);

        long val;
        while ((val = Interlocked.Read(ref state)) <= 0)
            await Task.Delay(100);
        watcher.Dispose();
        return val == 1;
    }

    private struct ScreenCastResponse
    {
        public uint Response;
        public Dictionary<string, object> Results;
    }

    public override void Dispose()
    {
        _dbus.Dispose();
        base.Dispose();
    }
}