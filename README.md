# WlxOverlay
A lightweight OpenVR overlay for Wayland and X11 desktops, inspired by XSOverlay.

Primarily made this because I couldn't find a proper desktop overlay for Linux.

Formerly known as X11Overlay

Features:
- Access your screens from within OpenVR / SteamVR
- Works with a huge variety of setups, including tiling window managers.
- Mouse pointer that supports left/right/middle click
- Customizable keyboard with 2-hand typing
- Watch panel that shows:
  - Local time + 2 customizable time zones
  - Battery states of SteamVR controllers + all connected trackers 
  - Volume rocker (customizable)
  - Toggles for screens / keyboard
- Notifications system with support for VRCX and Dbus (Desktop) notifications

![Image](https://github.com/galister/X11Overlay/blob/github/screenshot2.jpeg?raw=true)

# Getting Started

[Check out the Wiki here!](https://github.com/galister/X11Overlay/wiki/Getting-Started)

Join the discussion here: https://discord.gg/gHwJ2vwSWV

# Known Issues
- Dragging curved displays very close may make them disappear. Long click the toggle on the watch to force respawn.
- Wayland: It's possible that your compositor does not implement some of the required protocols. Please create a ticket to let us know in that case.
- Wayland: screencopy can crash, especially with multiple screens up. this is being investigated, recommend wrapping in a restart loop for practical use in the meantime.

# Works Used
- [FreeTypeSharp](https://github.com/ryancheung/FreeTypeSharp), MIT License
- [Godot Engine](https://github.com/godotengine/godot), MIT License
- [Liberation Fonts](https://github.com/liberationfonts/liberation-fonts), SIL Open Font License v1.1
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json), MIT License
- [OBS Studio](https://github.com/obsproject/obs-studio), GPLv2 License
- [OpenVR SDK](https://github.com/ValveSoftware/openvr), BSD-3-Clause license
- [OVRSharp](https://github.com/OVRTools/OVRSharp), MIT License
- [Silk.NET](https://github.com/dotnet/Silk.NET), MIT License
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp), Apache v2 License
- [bendahl/uinput](https://github.com/bendahl/uinput), MIT License
- [WaylandSharp](https://github.com/X9VoiD/WaylandSharp), MIT License
- [YamlDotNet](SixLabors/ImageSharp), MIT License
- [freesound](https://freesound.org/), CC0 sound effects (find the sounds by searching for their number)
