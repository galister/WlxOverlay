namespace MutterDisplayConfig.DBus
{
    using System;
    using Tmds.DBus.Protocol;
    using SafeHandle = System.Runtime.InteropServices.SafeHandle;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    record DisplayConfigProperties
    {
        public int PowerSaveMode { get; set; } = default!;
        public bool PanelOrientationManaged { get; set; } = default!;
        public bool ApplyMonitorsConfigAllowed { get; set; } = default!;
        public bool NightLightSupported { get; set; } = default!;
    }
    partial class DisplayConfig : MutterDisplayConfigObject
    {
        private const string __Interface = "org.gnome.Mutter.DisplayConfig";
        public DisplayConfig(MutterDisplayConfigService service, ObjectPath path) : base(service, path)
        { }
        public Task<(uint Serial, (uint, long, int, int, int, int, int, uint, uint[], Dictionary<string, object>)[] Crtcs, (uint, long, int, uint[], string, uint[], uint[], Dictionary<string, object>)[] Outputs, (uint, long, uint, uint, double, uint)[] Modes, int MaxScreenWidth, int MaxScreenHeight)> GetResourcesAsync()
        {
            return this.Connection.CallMethodAsync(CreateMessage(), (Message m, object? s) => ReadMessage_uaruxiiiiiuauaesvzaruxiausauauaesvzaruxuuduzii(m, (MutterDisplayConfigObject)s!), this);
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    member: "GetResources");
                return writer.CreateMessage();
            }
        }
        public Task ApplyConfigurationAsync(uint serial, bool persistent, (uint, int, int, int, uint, uint[], Dictionary<string, object>)[] crtcs, (uint, Dictionary<string, object>)[] outputs)
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    signature: "uba(uiiiuaua{sv})a(ua{sv})",
                    member: "ApplyConfiguration");
                writer.WriteUInt32(serial);
                writer.WriteBool(persistent);
                writer.WriteArray(crtcs);
                writer.WriteArray(outputs);
                return writer.CreateMessage();
            }
        }
        public Task<int> ChangeBacklightAsync(uint serial, uint output, int value)
        {
            return this.Connection.CallMethodAsync(CreateMessage(), (Message m, object? s) => ReadMessage_i(m, (MutterDisplayConfigObject)s!), this);
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    signature: "uui",
                    member: "ChangeBacklight");
                writer.WriteUInt32(serial);
                writer.WriteUInt32(output);
                writer.WriteInt32(value);
                return writer.CreateMessage();
            }
        }
        public Task<(ushort[] Red, ushort[] Green, ushort[] Blue)> GetCrtcGammaAsync(uint serial, uint crtc)
        {
            return this.Connection.CallMethodAsync(CreateMessage(), (Message m, object? s) => ReadMessage_aqaqaq(m, (MutterDisplayConfigObject)s!), this);
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    signature: "uu",
                    member: "GetCrtcGamma");
                writer.WriteUInt32(serial);
                writer.WriteUInt32(crtc);
                return writer.CreateMessage();
            }
        }
        public Task SetCrtcGammaAsync(uint serial, uint crtc, ushort[] red, ushort[] green, ushort[] blue)
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    signature: "uuaqaqaq",
                    member: "SetCrtcGamma");
                writer.WriteUInt32(serial);
                writer.WriteUInt32(crtc);
                writer.WriteArray(red);
                writer.WriteArray(green);
                writer.WriteArray(blue);
                return writer.CreateMessage();
            }
        }
        public Task<(uint Serial, ((string, string, string, string), (string, int, int, double, double, double[], Dictionary<string, object>)[], Dictionary<string, object>)[] Monitors, (int, int, double, uint, bool, (string, string, string, string)[], Dictionary<string, object>)[] LogicalMonitors, Dictionary<string, object> Properties)> GetCurrentStateAsync()
        {
            return this.Connection.CallMethodAsync(CreateMessage(), (Message m, object? s) => ReadMessage_uarrsssszarsiiddadaesvzaesvzariidubarsssszaesvzaesv(m, (MutterDisplayConfigObject)s!), this);
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    member: "GetCurrentState");
                return writer.CreateMessage();
            }
        }
        public Task ApplyMonitorsConfigAsync(uint serial, uint @method, (int, int, double, uint, bool, (string, string, Dictionary<string, object>)[])[] logicalMonitors, Dictionary<string, object> properties)
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    signature: "uua(iiduba(ssa{sv}))a{sv}",
                    member: "ApplyMonitorsConfig");
                writer.WriteUInt32(serial);
                writer.WriteUInt32(@method);
                writer.WriteArray(logicalMonitors);
                writer.WriteDictionary(properties);
                return writer.CreateMessage();
            }
        }
        public Task SetOutputCTMAsync(uint serial, uint output, (ulong, ulong, ulong, ulong, ulong, ulong, ulong, ulong, ulong) ctm)
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: __Interface,
                    signature: "uu(ttttttttt)",
                    member: "SetOutputCTM");
                writer.WriteUInt32(serial);
                writer.WriteUInt32(output);
                writer.WriteStruct(ctm);
                return writer.CreateMessage();
            }
        }
        public ValueTask<IDisposable> WatchMonitorsChangedAsync(Action<Exception?> handler, bool emitOnCapturedContext = true)
            => base.WatchSignalAsync(Service.Destination, __Interface, Path, "MonitorsChanged", handler, emitOnCapturedContext);
        public Task SetPowerSaveModeAsync(int value)
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: "org.freedesktop.DBus.Properties",
                    signature: "ssv",
                    member: "Set");
                writer.WriteString(__Interface);
                writer.WriteString("PowerSaveMode");
                writer.WriteSignature("i");
                writer.WriteInt32(value);
                return writer.CreateMessage();
            }
        }
        public Task SetPanelOrientationManagedAsync(bool value)
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: "org.freedesktop.DBus.Properties",
                    signature: "ssv",
                    member: "Set");
                writer.WriteString(__Interface);
                writer.WriteString("PanelOrientationManaged");
                writer.WriteSignature("b");
                writer.WriteBool(value);
                return writer.CreateMessage();
            }
        }
        public Task SetApplyMonitorsConfigAllowedAsync(bool value)
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: "org.freedesktop.DBus.Properties",
                    signature: "ssv",
                    member: "Set");
                writer.WriteString(__Interface);
                writer.WriteString("ApplyMonitorsConfigAllowed");
                writer.WriteSignature("b");
                writer.WriteBool(value);
                return writer.CreateMessage();
            }
        }
        public Task SetNightLightSupportedAsync(bool value)
        {
            return this.Connection.CallMethodAsync(CreateMessage());
            MessageBuffer CreateMessage()
            {
                using var writer = this.Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Service.Destination,
                    path: Path,
                    @interface: "org.freedesktop.DBus.Properties",
                    signature: "ssv",
                    member: "Set");
                writer.WriteString(__Interface);
                writer.WriteString("NightLightSupported");
                writer.WriteSignature("b");
                writer.WriteBool(value);
                return writer.CreateMessage();
            }
        }
        public Task<int> GetPowerSaveModeAsync()
            => this.Connection.CallMethodAsync(CreateGetPropertyMessage(__Interface, "PowerSaveMode"), (Message m, object? s) => ReadMessage_v_i(m, (MutterDisplayConfigObject)s!), this);
        public Task<bool> GetPanelOrientationManagedAsync()
            => this.Connection.CallMethodAsync(CreateGetPropertyMessage(__Interface, "PanelOrientationManaged"), (Message m, object? s) => ReadMessage_v_b(m, (MutterDisplayConfigObject)s!), this);
        public Task<bool> GetApplyMonitorsConfigAllowedAsync()
            => this.Connection.CallMethodAsync(CreateGetPropertyMessage(__Interface, "ApplyMonitorsConfigAllowed"), (Message m, object? s) => ReadMessage_v_b(m, (MutterDisplayConfigObject)s!), this);
        public Task<bool> GetNightLightSupportedAsync()
            => this.Connection.CallMethodAsync(CreateGetPropertyMessage(__Interface, "NightLightSupported"), (Message m, object? s) => ReadMessage_v_b(m, (MutterDisplayConfigObject)s!), this);
        public Task<DisplayConfigProperties> GetPropertiesAsync()
        {
            return this.Connection.CallMethodAsync(CreateGetAllPropertiesMessage(__Interface), (Message m, object? s) => ReadMessage(m, (MutterDisplayConfigObject)s!), this);
            static DisplayConfigProperties ReadMessage(Message message, MutterDisplayConfigObject _)
            {
                var reader = message.GetBodyReader();
                return ReadProperties(ref reader);
            }
        }
        public ValueTask<IDisposable> WatchPropertiesChangedAsync(Action<Exception?, PropertyChanges<DisplayConfigProperties>> handler, bool emitOnCapturedContext = true)
        {
            return base.WatchPropertiesChangedAsync(__Interface, (Message m, object? s) => ReadMessage(m, (MutterDisplayConfigObject)s!), handler, emitOnCapturedContext);
            static PropertyChanges<DisplayConfigProperties> ReadMessage(Message message, MutterDisplayConfigObject _)
            {
                var reader = message.GetBodyReader();
                reader.ReadString(); // interface
                List<string> changed = new(), invalidated = new();
                return new PropertyChanges<DisplayConfigProperties>(ReadProperties(ref reader, changed), changed.ToArray(), ReadInvalidated(ref reader));
            }
            static string[] ReadInvalidated(ref Reader reader)
            {
                List<string>? invalidated = null;
                ArrayEnd headersEnd = reader.ReadArrayStart(DBusType.String);
                while (reader.HasNext(headersEnd))
                {
                    invalidated ??= new();
                    var property = reader.ReadString();
                    switch (property)
                    {
                        case "PowerSaveMode": invalidated.Add("PowerSaveMode"); break;
                        case "PanelOrientationManaged": invalidated.Add("PanelOrientationManaged"); break;
                        case "ApplyMonitorsConfigAllowed": invalidated.Add("ApplyMonitorsConfigAllowed"); break;
                        case "NightLightSupported": invalidated.Add("NightLightSupported"); break;
                    }
                }
                return invalidated?.ToArray() ?? Array.Empty<string>();
            }
        }
        private static DisplayConfigProperties ReadProperties(ref Reader reader, List<string>? changedList = null)
        {
            var props = new DisplayConfigProperties();
            ArrayEnd headersEnd = reader.ReadArrayStart(DBusType.Struct);
            while (reader.HasNext(headersEnd))
            {
                var property = reader.ReadString();
                switch (property)
                {
                    case "PowerSaveMode":
                        reader.ReadSignature("i");
                        props.PowerSaveMode = reader.ReadInt32();
                        changedList?.Add("PowerSaveMode");
                        break;
                    case "PanelOrientationManaged":
                        reader.ReadSignature("b");
                        props.PanelOrientationManaged = reader.ReadBool();
                        changedList?.Add("PanelOrientationManaged");
                        break;
                    case "ApplyMonitorsConfigAllowed":
                        reader.ReadSignature("b");
                        props.ApplyMonitorsConfigAllowed = reader.ReadBool();
                        changedList?.Add("ApplyMonitorsConfigAllowed");
                        break;
                    case "NightLightSupported":
                        reader.ReadSignature("b");
                        props.NightLightSupported = reader.ReadBool();
                        changedList?.Add("NightLightSupported");
                        break;
                    default:
                        reader.ReadVariant();
                        break;
                }
            }
            return props;
        }
    }
    partial class MutterDisplayConfigService
    {
        public Tmds.DBus.Protocol.Connection Connection { get; }
        public string Destination { get; }
        public MutterDisplayConfigService(Tmds.DBus.Protocol.Connection connection, string destination)
            => (Connection, Destination) = (connection, destination);
        public DisplayConfig CreateDisplayConfig(string path) => new DisplayConfig(this, path);
    }
    class MutterDisplayConfigObject
    {
        public MutterDisplayConfigService Service { get; }
        public ObjectPath Path { get; }
        protected Tmds.DBus.Protocol.Connection Connection => Service.Connection;
        protected MutterDisplayConfigObject(MutterDisplayConfigService service, ObjectPath path)
            => (Service, Path) = (service, path);
        protected MessageBuffer CreateGetPropertyMessage(string @interface, string property)
        {
            using var writer = this.Connection.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: Service.Destination,
                path: Path,
                @interface: "org.freedesktop.DBus.Properties",
                signature: "ss",
                member: "Get");
            writer.WriteString(@interface);
            writer.WriteString(property);
            return writer.CreateMessage();
        }
        protected MessageBuffer CreateGetAllPropertiesMessage(string @interface)
        {
            using var writer = this.Connection.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: Service.Destination,
                path: Path,
                @interface: "org.freedesktop.DBus.Properties",
                signature: "s",
                member: "GetAll");
            writer.WriteString(@interface);
            return writer.CreateMessage();
        }
        protected ValueTask<IDisposable> WatchPropertiesChangedAsync<TProperties>(string @interface, MessageValueReader<PropertyChanges<TProperties>> reader, Action<Exception?, PropertyChanges<TProperties>> handler, bool emitOnCapturedContext)
        {
            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Sender = Service.Destination,
                Path = Path,
                Interface = "org.freedesktop.DBus.Properties",
                Member = "PropertiesChanged",
                Arg0 = @interface
            };
            return this.Connection.AddMatchAsync(rule, reader,
                                                    (Exception? ex, PropertyChanges<TProperties> changes, object? rs, object? hs) => ((Action<Exception?, PropertyChanges<TProperties>>)hs!).Invoke(ex, changes),
                                                    this, handler, emitOnCapturedContext);
        }
        public ValueTask<IDisposable> WatchSignalAsync<TArg>(string sender, string @interface, ObjectPath path, string signal, MessageValueReader<TArg> reader, Action<Exception?, TArg> handler, bool emitOnCapturedContext)
        {
            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Sender = sender,
                Path = path,
                Member = signal,
                Interface = @interface
            };
            return this.Connection.AddMatchAsync(rule, reader,
                                                    (Exception? ex, TArg arg, object? rs, object? hs) => ((Action<Exception?, TArg>)hs!).Invoke(ex, arg),
                                                    this, handler, emitOnCapturedContext);
        }
        public ValueTask<IDisposable> WatchSignalAsync(string sender, string @interface, ObjectPath path, string signal, Action<Exception?> handler, bool emitOnCapturedContext)
        {
            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Sender = sender,
                Path = path,
                Member = signal,
                Interface = @interface
            };
            return this.Connection.AddMatchAsync<object>(rule, (Message message, object? state) => null!,
                                                            (Exception? ex, object v, object? rs, object? hs) => ((Action<Exception?>)hs!).Invoke(ex), this, handler, emitOnCapturedContext);
        }
        protected static (uint, (uint, long, int, int, int, int, int, uint, uint[], Dictionary<string, object>)[], (uint, long, int, uint[], string, uint[], uint[], Dictionary<string, object>)[], (uint, long, uint, uint, double, uint)[], int, int) ReadMessage_uaruxiiiiiuauaesvzaruxiausauauaesvzaruxuuduzii(Message message, MutterDisplayConfigObject _)
        {
            var reader = message.GetBodyReader();
            var arg0 = reader.ReadUInt32();
            var arg1 = reader.ReadArray<(uint, long, int, int, int, int, int, uint, uint[], Dictionary<string, object>)>();
            var arg2 = reader.ReadArray<(uint, long, int, uint[], string, uint[], uint[], Dictionary<string, object>)>();
            var arg3 = reader.ReadArray<(uint, long, uint, uint, double, uint)>();
            var arg4 = reader.ReadInt32();
            var arg5 = reader.ReadInt32();
            return (arg0, arg1, arg2, arg3, arg4, arg5);
        }
        protected static int ReadMessage_i(Message message, MutterDisplayConfigObject _)
        {
            var reader = message.GetBodyReader();
            return reader.ReadInt32();
        }
        protected static (ushort[], ushort[], ushort[]) ReadMessage_aqaqaq(Message message, MutterDisplayConfigObject _)
        {
            var reader = message.GetBodyReader();
            var arg0 = reader.ReadArray<ushort>();
            var arg1 = reader.ReadArray<ushort>();
            var arg2 = reader.ReadArray<ushort>();
            return (arg0, arg1, arg2);
        }
        protected static (uint, ((string, string, string, string), (string, int, int, double, double, double[], Dictionary<string, object>)[], Dictionary<string, object>)[], (int, int, double, uint, bool, (string, string, string, string)[], Dictionary<string, object>)[], Dictionary<string, object>) ReadMessage_uarrsssszarsiiddadaesvzaesvzariidubarsssszaesvzaesv(Message message, MutterDisplayConfigObject _)
        {
            var reader = message.GetBodyReader();
            var arg0 = reader.ReadUInt32();
            var arg1 = reader.ReadArray<((string, string, string, string), (string, int, int, double, double, double[], Dictionary<string, object>)[], Dictionary<string, object>)>();
            var arg2 = reader.ReadArray<(int, int, double, uint, bool, (string, string, string, string)[], Dictionary<string, object>)>();
            var arg3 = reader.ReadDictionary<string, object>();
            return (arg0, arg1, arg2, arg3);
        }
        protected static int ReadMessage_v_i(Message message, MutterDisplayConfigObject _)
        {
            var reader = message.GetBodyReader();
            reader.ReadSignature("i");
            return reader.ReadInt32();
        }
        protected static bool ReadMessage_v_b(Message message, MutterDisplayConfigObject _)
        {
            var reader = message.GetBodyReader();
            reader.ReadSignature("b");
            return reader.ReadBool();
        }
    }
    class PropertyChanges<TProperties>
    {
        public PropertyChanges(TProperties properties, string[] invalidated, string[] changed)
        	=> (Properties, Invalidated, Changed) = (properties, invalidated, changed);
        public TProperties Properties { get; }
        public string[] Invalidated { get; }
        public string[] Changed { get; }
        public bool HasChanged(string property) => Array.IndexOf(Changed, property) != -1;
        public bool IsInvalidated(string property) => Array.IndexOf(Invalidated, property) != -1;
    }
}
