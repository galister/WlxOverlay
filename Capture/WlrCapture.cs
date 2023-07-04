using WaylandSharp;
using WlxOverlay.Capture.Wlr;
using WlxOverlay.Core.Subsystem;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.GFX;

namespace WlxOverlay.Capture;

public class WlrCapture<T> : IDesktopCapture where T: IWlrFrame
{
    private readonly TimeSpan RoundTripSleepTime = TimeSpan.FromMilliseconds(1);

    private readonly WaylandOutput _screen;
    private readonly WlDisplay _display;
    private readonly WlrCaptureData _data;
    
    private IWlrFrame? _frame;
    private IWlrFrame? _lastFrame;
    
    private readonly CancellationTokenSource _cancel = new();
    private Task? _worker;

    public WlrCapture(WaylandOutput output)
    {
        _display = WlDisplay.Connect(WaylandSubsystem.DisplayName!);

        var reg = _display.GetRegistry();
        _data = new WlrCaptureData();
        _screen = output;

        reg.Global += (_, e) =>
        {
            if (e.Interface == WlInterface.WlOutput.Name)
            {
                if (e.Name == output.IdName) 
                    _data.Output = reg.Bind<WlOutput>(e.Name, e.Interface, e.Version);
            }
            else if (e.Interface == WlInterface.WlShm.Name)
                _data.Shm = reg.Bind<WlShm>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZwlrExportDmabufManagerV1.Name)
                _data.DmabufManager = reg.Bind<ZwlrExportDmabufManagerV1>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZwlrScreencopyManagerV1.Name)
                _data.ScreencopyManager = reg.Bind<ZwlrScreencopyManagerV1>(e.Name, e.Interface, e.Version);
        };

        reg.GlobalRemove += (_, e) =>
        {
            if (e.Name == _data.Output!.GetId())
                Dispose();
        };

        _display.Roundtrip();        
    }

    public void Initialize()
    {
        if (_data.Output == null)
        {
            Console.WriteLine($"FATAL Could not find WlOutput for {_screen.Name}!");
            throw new ApplicationException();
        }
        if (typeof(T) == typeof(DmaBufFrame) && _data.DmabufManager == null)
        {
            Console.WriteLine("FATAL Your Wayland compositor does not support " + WlInterface.ZwlrExportDmabufManagerV1.Name);
            Console.WriteLine("FATAL Check your `config.yaml`!");
            throw new ApplicationException();
        }
        if (typeof(T) == typeof(ScreenCopyFrame) && (_data.ScreencopyManager == null || _data.Shm == null))
        {
            Console.WriteLine("FATAL Your Wayland compositor does not support " + WlInterface.ZwlrScreencopyManagerV1.Name);
            Console.WriteLine("FATAL Check your `config.yaml`!");
            throw new ApplicationException();
        }
    }

    public bool TryApplyToTexture(ITexture texture)
    {
        var wantNewFrame = true;
        var retVal = false;

        if (_worker is { Status: TaskStatus.RanToCompletion })
        {
            _worker.Dispose();
            switch (_frame!.GetStatus())
            {
                case CaptureStatus.FrameReady:
                    _frame.ApplyToTexture(texture);
                    retVal = true;
                    break;
                case CaptureStatus.FrameSkipped:
                    Console.WriteLine($"{_screen.Name}: Frame was skipped.");
                    break;
                case CaptureStatus.Fatal:
                    Console.WriteLine($"{_screen.Name}: Fatal error occurred.");
                    Dispose();
                    return false;
            }
        }
        else if (_worker != null)
            wantNewFrame = false;

        if (wantNewFrame)
            _worker = Task.Run(RequestNewFrame, _cancel.Token);
        return retVal;
    }

    public void Pause()
    {
        _lastFrame?.Dispose();
        _frame?.Dispose();
        _lastFrame = null;
        _frame = null;
        _worker = null;
    }

    public void Resume()
    {
    }

    public void Dispose()
    {
        Pause();
        _display.Dispose();
        _data.Dispose();
    }
    
    private void RequestNewFrame()
    {
        _lastFrame?.Dispose();
        _lastFrame = _frame;
        _frame = (IWlrFrame?)Activator.CreateInstance(typeof(T), _data);
        _display.Roundtrip();
        while (_frame?.GetStatus() == CaptureStatus.Pending)
        {
            Thread.Sleep(RoundTripSleepTime);
            _display.Roundtrip();
        }
    }
}

public sealed class WlrCaptureData : IDisposable
{
    public WlOutput? Output;
    public ZwlrExportDmabufManagerV1? DmabufManager;
    public ZwlrScreencopyManagerV1? ScreencopyManager;
    public WlShm? Shm;

    public void Dispose()
    {
        Output!.Dispose();
        DmabufManager?.Dispose();
        ScreencopyManager?.Dispose();
        Shm?.Dispose();
    }
}