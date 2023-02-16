using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Screen.X11;
using X11Overlay.Types;
using Action = System.Action;

namespace X11Overlay.UI;

public class KeyButton : ButtonBase
{
    private const int ModeNormal = 0;
    private const int ModeShift = 1;
    private const int ModeAlt = 2;
    
    public static int Mode = 0;

    private static readonly HashSet<VirtualKey> Modifiers = new(8);
    
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

    private readonly VirtualKey[] _myKeyCodes = new VirtualKey[3];
    
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

            SetActionsForKey(key, ModeNormal, KeyModifier.None);

            labelTexts = layout.LabelForKey(key, true);
            for (var i = 0; i < labelTexts.Length; i++)
                _labelTexts[ModeShift, i] = labelTexts[i];

            SetActionsForKey(key, ModeShift, KeyModifier.Shift);
        }

        var altModifier = KeyModifier.None;

        if (layout.AltLayoutMode == AltLayoutMode.Layout)
            key = layout.AltLayout[row][col];
        else if (layout.AltLayoutMode == AltLayoutMode.Ctrl)
            altModifier = KeyModifier.Ctrl;
        else if (layout.AltLayoutMode == AltLayoutMode.Meta)
            altModifier = KeyModifier.Meta;
        else if (layout.AltLayoutMode == AltLayoutMode.Shift)
            altModifier = KeyModifier.Shift;
        else if (layout.AltLayoutMode == AltLayoutMode.Super)
            altModifier = KeyModifier.Super;

        if (key != null)
        {
            _visibility[2] = true;

            var labelTexts = layout.LabelForKey(key);
            for (var i = 0; i < labelTexts.Length; i++)
                _labelTexts[ModeAlt, i] = labelTexts[i];

            SetActionsForKey(key, ModeAlt, altModifier);
        }
    }

    private void SetActionsForKey(string? key, int mode, KeyModifier modifier)
    {
        if (key == null)
            return;

        if (Enum.TryParse(key, out VirtualKey vk))
        {
            if (KeyboardLayout.Instance.Modifiers.Contains(vk))
            {
                _myKeyCodes[mode] = vk;
                _pressActions[mode] = () => OnModifierPressed(vk);
                _releaseActions[mode] = () => OnModifierReleased(vk);
            }
            else
            {
                _pressActions[mode] = () => OnKeyPressed(vk, modifier);
                _releaseActions[mode] = () => OnKeyReleased(vk, modifier);
            }
        }
        else if (KeyboardLayout.Instance.Macros.TryGetValue(key, out var macro))
        {
            var events = KeyboardLayout.Instance.KeyEventsFromMacro(macro);
            _pressActions[mode] = () => events.ForEach(e => XScreenCapture.SendKey((int) e.key, e.down));
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

    private void OnKeyPressed(VirtualKey key, KeyModifier modifier)
    {
        if (modifier != KeyModifier.None)
        {
            Modifiers.Add(KeyboardLayout.ModifierKeys[modifier].First());
            foreach (var modKey in KeyboardLayout.ModifierKeys[modifier].Skip(1))
                Modifiers.Remove(modKey);
        }

        foreach (var mod in Modifiers)
            XScreenCapture.SendKey((int) mod, true);
            
        XScreenCapture.SendKey((int) key, true);
    }
        
    private void OnKeyReleased(VirtualKey key, KeyModifier modifier)
    {
        if (modifier != KeyModifier.None)
        {
            Modifiers.Add(KeyboardLayout.ModifierKeys[modifier].First());
            foreach (var modKey in KeyboardLayout.ModifierKeys[modifier].Skip(1))
                Modifiers.Remove(modKey);
        }
            
        XScreenCapture.SendKey((int) key, false);

        foreach (var mod in Modifiers) 
            XScreenCapture.SendKey((int) mod, false);
        Modifiers.Clear();
    }
    
    private void OnModifierPressed(VirtualKey key)
    {
        XScreenCapture.SendKey((int) key, true);
    }
    
    private void OnModifierReleased(VirtualKey key)
    {
        XScreenCapture.SendKey((int) key, false);

        if (Modifiers.Contains(key))
        {
            Modifiers.Remove(key);
        }
        else
        {
            Modifiers.Add(key);
        }
    }
}