using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Screen.Interop;
using X11Overlay.Types;
using Action = System.Action;

namespace X11Overlay.UI;

public class KeyButton : ButtonBase
{
    private const int ModeNormal = 0;
    private const int ModeShift = 1;
    private const int ModeAlt = 2;
    
    public static int Mode = 0;

    private static readonly HashSet<int> Modifiers = new(8);
    
    private readonly Label _label2;
    
    private readonly string[,] _labelTexts = new string[3,2];

    private readonly Vector3[] _modeColors =
    {
        HexColor.FromRgb("#006080"), 
        HexColor.FromRgb("#b03000"), 
        HexColor.FromRgb("#600080")
    };

    private readonly bool[] _visibility = new bool[3];
    
    private readonly Action?[] _pressActions = new Action[3];
    private readonly Action?[] _releaseActions = new Action[3];

    private readonly int[] _myKeyCodes = new int[3];
    
    public KeyButton(uint row, uint col, int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        var fontSize = Canvas.CurrentFont!.Size();
        
        _label = new Label(null, x+4, y+(int)h-fontSize-4, w, h);
        _label2 = new Label(null, x+(int)(w/2), y+4+fontSize, w, h);
        
        var layout = KeyboardLayout.Instance;
        var key = layout.MainLayout[row][col];

        if (key != null)
        {
            _visibility[0] = _visibility[1] = true;
            
            var labelTexts = layout.LabelForKey(key);
            for (var i = 0; i < labelTexts.Length; i++)
                _labelTexts[ModeNormal, i] = labelTexts[i];

            SetActionsForKey(key, ModeNormal);

            labelTexts = layout.LabelForKey(key, true);
            for (var i = 0; i < labelTexts.Length; i++)
                _labelTexts[ModeShift, i] = labelTexts[i];

            SetActionsForKey(key, ModeShift);
        }

        key = layout.AltLayout[row][col];

        if (key != null)
        {
            _visibility[2] = true;

            var labelTexts = layout.LabelForKey(key);
            for (var i = 0; i < labelTexts.Length; i++)
                _labelTexts[ModeAlt, i] = labelTexts[i];

            SetActionsForKey(key, ModeAlt);
        }
    }

    private void SetActionsForKey(string? key, int mode)
    {
        if (key == null)
            return;

        if (KeyboardLayout.Instance.Keycodes.TryGetValue(key, out var keyCode))
        {
            if (KeyboardLayout.Instance.Modifiers.Contains(keyCode))
            {
                _myKeyCodes[mode] = keyCode;
                _pressActions[mode] = () => OnModifierPressed(keyCode);
                _releaseActions[mode] = () => OnModifierReleased(keyCode);
            }
            else
            {
                _pressActions[mode] = () => OnKeyPressed(keyCode, mode == ModeShift);
                _releaseActions[mode] = () => OnKeyReleased(keyCode, mode == ModeShift);
            }
        }
        else if (KeyboardLayout.Instance.Macros.TryGetValue(key, out var macro))
        {
            var events = KeyboardLayout.Instance.KeyEventsFromMacro(macro);
            _pressActions[mode] = () => events.ForEach(e => XScreenCapture.SendKey(e.key, e.down));
        }

        else if (KeyboardLayout.Instance.ExecCommands.TryGetValue(key, out var argv))
        {
            var psi = Runner.StartInfoFromArgs(argv);
            if (psi != null)
                _pressActions[mode] = () => Runner.TryStart(psi);
        }
    }

    public override void OnPointerDown()
    {
        base.OnPointerDown();
        _pressActions[Mode]?.Invoke();
    }

    public override void OnPointerUp()
    {
        base.OnPointerUp();
        _releaseActions[Mode]?.Invoke();
    }

    public override void SetCanvas(Canvas canvas)
    {
        base.SetCanvas(canvas);
        _label2.SetCanvas(canvas);
    }

    public override void Update()
    {
        base.Update();
        if (Modifiers.Contains(_myKeyCodes[Mode]))
        {
            if (!IsClicked)
            {
                IsClicked = true;
                Canvas!.MarkDirty();
            }
        }
        else
        {
            if (IsClicked)
            {
                IsClicked = false;
                Canvas!.MarkDirty();
            }
        }
    }

    public override void Render()
    {
        if (!_visibility[Mode])
            return;
        
        _label.Text = _labelTexts[Mode, 0];
        _label2.Text = _labelTexts[Mode, 1];
        _label2.FgColor = _label.FgColor = _modeColors[Mode];
        
        base.Render();
        _label2.Render();
    }

    private void OnKeyPressed(int keycode, bool shift)
    {
        if (shift)
        {
            Modifiers.Remove(KeyboardLayout.Instance.ShiftKeyCodes[0]);
            Modifiers.Add(KeyboardLayout.Instance.ShiftKeyCodes[1]);
        }

        foreach (var mod in Modifiers)
            XScreenCapture.SendKey(mod, true);
            
        XScreenCapture.SendKey(keycode, true);
    }
        
    private void OnKeyReleased(int keycode, bool shift)
    {
        if (shift)
        {
            Modifiers.Remove(KeyboardLayout.Instance.ShiftKeyCodes[0]);
            Modifiers.Add(KeyboardLayout.Instance.ShiftKeyCodes[1]);
        }
            
        XScreenCapture.SendKey(keycode, false);

        foreach (var mod in Modifiers) 
            XScreenCapture.SendKey(mod, false);
        Modifiers.Clear();
    }
    
    private void OnModifierPressed(int keyCode)
    {
        XScreenCapture.SendKey(keyCode, true);
    }
    
    private void OnModifierReleased(int keycode)
    {
        XScreenCapture.SendKey(keycode, false);

        if (Modifiers.Contains(keycode))
        {
            Modifiers.Remove(keycode);
        }
        else
        {
            Modifiers.Add(keycode);
        }
    }
}