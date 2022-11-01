using System.Diagnostics;

namespace X11Overlay.Overlays;

/// <summary>
/// A laser pointer with a PTT button
/// </summary>
public class LaserPointerWithPushToTalk : LaserPointer
{
    public string? PttCommandOn;
    public string? PttCommandOff;

    public LaserPointerWithPushToTalk(LeftRight hand) : base(hand)
    {
    }

    protected internal override void AfterInput()
    {
        base.AfterInput();

        if (AltClickNow && !AltClickBefore)
            Ptt(true);
        else if (!AltClickNow && AltClickBefore) 
            Ptt(false);

    }
    
    private void Ptt(bool on)
    {
        var cmd = on ? PttCommandOn : PttCommandOff;

        var splat = cmd!.Split(' ', 2);
        
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = splat[0],
            Arguments = splat.Length > 1 ? splat[1] : ""
        });
    }
}