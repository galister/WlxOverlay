using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Tmds.DBus.Protocol;
using WlxOverlay.Overlays;
using WlxOverlay.Types;

namespace WlxOverlay.Core;

public class NotificationsManager : IDisposable
{
    private static NotificationsManager _instance = null!;

    private readonly IPEndPoint _listenEndpoint;
    private readonly Socket _listenSocket;
    private readonly byte[] _listenBuffer = new byte[1024 * 16];

    private readonly CancellationTokenSource _cancel = new();
    private Task? _udpListener;
    private Task? _notifier;

    private readonly object _lockObject = new();
    private readonly Queue<XSOMessage> _messages = new(10);

    private DateTime _nextToast = DateTime.MinValue;
    private string? _notificationSound;

    public static void Initialize()
    {
        _instance = new NotificationsManager();
        _instance.Start();
    }

    public static void Toast(string title, string content, float timeout = 10)
    {
        lock (_instance._lockObject)
            _instance._messages.Enqueue(new XSOMessage
            {
                timeout = timeout,
                height = 110f,
                messageType = 1,
                content = content,
                title = title,
                opacity = 1
            });
    }

    private NotificationsManager()
    {
        try
        {
            _listenEndpoint = IPEndPoint.Parse(Config.Instance.NotificationsEndpoint);
        }
        catch
        {
            Console.WriteLine("FATAL: Could not parse config.yaml entry: notifications_endpoint");
            throw;
        }
        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        if ((Config.Instance.NotificationsVolume ?? 1) > float.Epsilon)
        {
            _notificationSound = Config.Instance.NotificationsSound == null
                ? Path.Combine(Config.ResourcesFolder, "660533.wav")
                : Path.Combine(Config.UserConfigFolder, Config.Instance.NotificationsSound);
        }
    }

    private void Start()
    {
        var _ = RegisterDbusAsync();
        _udpListener = UdpListenerAsync(_cancel.Token);
        _notifier = NotifierAsync(_cancel.Token);
    }

    private async Task RegisterDbusAsync()
    {
        var dbus = Connection.Session;

        var rule = new MatchRule
        {
            Member = "Notify",
            Interface = "org.freedesktop.Notifications",
            Path = "/org/freedesktop/Notifications",
            Type = MessageType.MethodCall,
            Eavesdrop = true
        };

        await dbus.AddMatchAsync(rule, (m, _) =>
        {
            var reader = m.GetBodyReader();
            var unused = reader.ReadString();
            var unused1 = reader.ReadUInt32();
            var unused2 = reader.ReadString();
            var summary = reader.ReadString();
            var body = reader.ReadString();

            return new XSOMessage
            {
                timeout = 3,
                height = 110f,
                messageType = 1,
                content = body,
                title = summary,
                opacity = 1,
                audioPath = "default",
                volume = Config.Instance.NotificationsVolume ?? 1,
            };

        }, (e, t, _, _) =>
        {
            if (e != null)
                return;

            lock (_lockObject)
                _messages.Enqueue(t);
        });
    }

    private async Task UdpListenerAsync(CancellationToken _cancellationToken)
    {
        try
        {
            try
            {
                _listenSocket.Bind(_listenEndpoint);
                Console.WriteLine($"Listening for notifications @ {_listenEndpoint.Address}:{_listenEndpoint.Port}");
            }
            catch
            {
                Console.WriteLine($"Could not listen for notifications @ {_listenEndpoint.Address}:{_listenEndpoint.Port}");
                throw;
            }
            var remoteEp = new IPEndPoint(IPAddress.Any, 0);
            while (_listenSocket.IsBound && !_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _listenSocket.ReceiveFromAsync(_listenBuffer, SocketFlags.None, remoteEp);
                    var receivedBytes = new ArraySegment<byte>(_listenBuffer, 0, result.ReceivedBytes);
                    BytesReceived(receivedBytes);
                }
                catch (Exception x)
                {
                    Console.WriteLine(x.ToString());
                }
            }
        }
        catch (Exception x)
        {
            Console.WriteLine(x);
        }
    }

    private void BytesReceived(ArraySegment<byte> bytes)
    {
        var json = Encoding.UTF8.GetString(bytes);
        var message = JsonConvert.DeserializeObject<XSOMessage>(json);

        if (message.messageType == 1)
            lock (_lockObject)
                _messages.Enqueue(message);
    }

    private async Task NotifierAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
            if (_nextToast > DateTime.UtcNow)
                continue;

            XSOMessage message;
            lock (_lockObject)
                if (!_messages.TryDequeue(out message))
                    continue;

            var toast = new Toast(message.title, message.content, message.opacity, (uint)message.height, message.timeout);
            OverlayManager.Instance.RegisterChild(toast);

            string? audioPath = null;
            if (message.audioPath == "default" || message.audioPath == "error" || message.audioPath == "warning") {
                audioPath = _notificationSound;
            } else if (File.Exists(message.audioPath)) {
                audioPath = message.audioPath;
            }

            if (audioPath != null && message.volume > 0.0f)
                _ = AudioManager.Instance.PlayAsync(audioPath, message.volume);

            _nextToast = DateTime.UtcNow.AddSeconds(2);
        }
    }

    public void Dispose()
    {
        _cancel.Cancel();

        _udpListener?.Dispose();
        _notifier?.Dispose();
        _cancel.Dispose();
        _listenSocket.Dispose();
    }
}

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
public struct XSOMessage
{
    public int messageType { get; set; }
    public int index { get; set; }
    public float volume { get; set; }
    public string audioPath { get; set; }
    public float timeout { get; set; }
    public string title { get; set; }
    public string? content { get; set; }
    public string? icon { get; set; }
    public float height { get; set; }
    public float opacity { get; set; }
    public bool useBase64Icon { get; set; }
    public string? sourceApp { get; set; }
}
