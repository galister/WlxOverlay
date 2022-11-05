using System.Diagnostics;
using X11Overlay.Types;

namespace X11Overlay.Overlays;

/// <summary>
/// A laser pointer with a PTT button
/// </summary>
public class LaserPointerWithPushToTalk : LaserPointer
{
    private readonly ProcessStartInfo?[] _processStartInfos = new ProcessStartInfo[2];
    
    public LaserPointerWithPushToTalk(LeftRight hand) : base(hand)
    {
        var dnCmd = hand == LeftRight.Left ? Config.Instance.LeftPttDnCmd : Config.Instance.RightPttDnCmd;
        var upCmd = hand == LeftRight.Left ? Config.Instance.LeftPttUpCmd : Config.Instance.RightPttUpCmd;
        
        _processStartInfos[0] = Runner.StartInfoFromArgs(dnCmd);
        _processStartInfos[1] = Runner.StartInfoFromArgs(upCmd);
    }

    protected internal override void AfterInput(bool batteryStateUpdated)
    {
        base.AfterInput(batteryStateUpdated);

        if (AltClickNow && !AltClickBefore)
            Ptt(true);
        else if (!AltClickNow && AltClickBefore) 
            Ptt(false);

    }
    
    private void Ptt(bool on)
    {
        var psi = _processStartInfos[on ? 1 : 0];
        if (psi != null)
            Process.Start(psi);
    }
}