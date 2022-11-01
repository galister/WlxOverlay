// See https://aka.ms/new-console-template for more information

using X11Overlay.Core;
using X11Overlay.GFX.OpenGL;
using X11Overlay.Overlays;

var manager = OverlayManager.Initialize();

manager.RegisterChild(new DesktopCursor());
manager.RegisterChild(new LaserPointer(LeftRight.Left));
manager.RegisterChild(new LaserPointerWithPushToTalk(LeftRight.Right) 
{ 
    PttCommandOn = "ptt B2_source on", 
    PttCommandOff = "ptt B2_source off"
});
manager.RegisterChild(new ScreenOverlay(0) { WantVisible = true });

var engine = new GlGraphicsEngine();
engine.StartEventLoop();