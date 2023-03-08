using System.Reflection;
using Desktop.DBus;
using Tmds.DBus.Protocol;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Desktop.Wayland;

internal static class XdgScreenCastHandler
{
    private static int _counter;
    
    public static async Task<XdgScreenData?> PromptUserAsync()
    {
        var data = new XdgScreenData(_counter++);
        if (await data.InitDbusAsync())
            return data;
        return null;
    }
}

internal class XdgScreenData : PipeWireScreenData, IDisposable
{
    private Connection _dbus = null!;
    private DesktopService _service = null!;
    private ScreenCast _screenCast = null!;

    private readonly string _token;
    private string? _requestPath;
    private string? _sessionPath;

    public XdgScreenData(int number)
    {
        _token = $"xdg_screen_{number}";
    }

    public async Task<bool> InitDbusAsync()
    {
        _dbus = new Connection(Address.Session!);
        await _dbus.ConnectAsync();

        var myName = _dbus.UniqueName!.Substring(1).Replace(".", "_");
        _requestPath = $"/org/freedesktop/portal/desktop/request/{myName}/{_token}";

        _service = new DesktopService(_dbus, "org.freedesktop.portal.Desktop");
        _screenCast = _service.CreateScreenCast("/org/freedesktop/portal/desktop");

        if (await CreateSessionAsync() && await SelectSourcesAsync() && await StartCaptureAsync())
        {
            Console.WriteLine("ScreenCast session started");
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
        
        bool? retVal = null;

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
                if (retVal.HasValue)
                    return;
                
                if (e != null)
                {
                    Console.WriteLine($"ERR Could not create ScreenCast session: {e.Message}");
                    retVal = false;
                    return;
                }
                
                if (t.Response != 0)
                {
                    Console.WriteLine($"ERR Could not create ScreenCast session: {t.Response}");
                    retVal = false;
                    return;
                }

                _sessionPath = t.Results["session_handle"] as string ?? throw new Exception("Invalid session_handle");
                retVal = true;
            },
            false);
        
        await _screenCast.CreateSessionAsync(options);

        while (!retVal.HasValue) 
            await Task.Delay(100);
        watcher.Dispose();
        return retVal.Value;
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

        bool? retVal = null;
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
                if (retVal.HasValue)
                    return;
                
                if (e != null)
                {
                    Console.WriteLine($"ERR Could not create ScreenCast session: {e.Message}");
                    retVal = false;
                    return;
                }
                if (t.Response != 0)
                {
                    Console.WriteLine($"ERR Could not select ScreenCast source: {t.Response}");
                    retVal = false;
                    return;
                }

                retVal = true;
            },
            false);
        
        await _screenCast.SelectSourcesAsync(_sessionPath!, options);

        while (!retVal.HasValue) 
            await Task.Delay(100);
        watcher.Dispose();
        return retVal.Value;
    }

    private async Task<bool> StartCaptureAsync()
    {
        var options = new Dictionary<string, object>
        {
            ["handle_token"] = _token,
        };

        bool? retVal = null;
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
                if (retVal.HasValue)
                    return;
                
                if (e != null)
                {
                    Console.WriteLine($"ERR Could not Start ScreenCast session: {e.Message}");
                    retVal = false;
                    return;
                }
                if (t.Response != 0)
                {
                    Console.WriteLine($"ERR Could not Start ScreenCast source: {t.Response}");
                    retVal = false;
                    return;
                }

                if (!t.Results.TryGetValue("streams", out var maybeStreams) 
                    || !(maybeStreams is ValueTuple<uint, Dictionary<string, object>>[] streams))
                {
                    Console.WriteLine($"ERR Could not Start ScreenCast source: Unexpected response");
                    retVal = false;
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
                
                NodeId = streams[0].Item1;
                Console.WriteLine(NodeId);
                if (streams[0].Item2["position"] is ValueTuple<int,int> pos)
                    Position = new Vector2Int(pos.Item1, pos.Item2);
                else
                {
                    Console.WriteLine($"ERR Could not Start ScreenCast source: Unexpected format: position");
                    retVal = false;
                    return;
                }
                
                if (streams[0].Item2["size"] is ValueTuple<int,int> size)
                    Size = new Vector2Int(size.Item1, size.Item2);
                else
                {
                    Console.WriteLine($"ERR Could not Start ScreenCast source: Unexpected format: size");
                    retVal = false;
                    return;
                }

                retVal = true;
            },
            false);
        
        await _screenCast.StartAsync(_sessionPath!, "", options);

        while (!retVal.HasValue) 
            await Task.Delay(100);
        watcher.Dispose();
        return retVal.Value;
    }
    
    private async Task<bool> OpenPipeWireRemoteAsync()
    {
        var options = new Dictionary<string, object>();

        var handle = await _screenCast.OpenPipeWireRemoteAsync(_sessionPath!, options);

        Fd = handle.DangerousGetHandle();
        return true;
    }

    private struct ScreenCastResponse
    {
        public uint Response;
        public Dictionary<string, object> Results;
    }

    public void Dispose()
    {
        _dbus.Dispose();
    }
}