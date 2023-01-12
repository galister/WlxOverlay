# X11Overlay
A lightweight X11 desktop overlay for OpenVR / SteamVR.

Primarily made this because I couldn't find a proper overlay for Linux.

A reimplementation of [OVR4X11](https://github.com/galister/OVR4X11) using a lighter toolkit.

Features:
- Access your screens from within OpenVR / SteamVR
- Mouse pointer that supports left/right/middle click, depending on hand orientation
- Customizable keyboard with 2-hand typing
- Notifications system (WIP)
- Watch panel that shows:
  - Local time + 2 customizable time zones
  - Battery states of controllers + all connected trackers (might get cramped with a lot of trackers, though)
  - Volume rocker
  - Toggles for screens / keyboard

# Getting Started

[Check out the Wiki here!](https://github.com/galister/X11Overlay/wiki/Getting-Started)

# Non-planned Features
- Displaying individual windows (XComposite) as this does not work well when using workspaces (windows getting culled and display black)
- Wayland support (at least until SteamVR runs reliably on Wayland)
- Windows support

# Known Issues
- Dragging curved displays very close may make them disappear. Long click the toggle on the watch to force respawn.
- App will not start with "invalid keyboard layout" if the keys in the config are dead on your layout. This can happen if you use a non-US layout for your physical keyboard. Workaround is to `setxkbmap us` before you start the app, or change the keyboard.yaml to reflect your local layout. (You may revert `setxkbmap` after launching the app.)

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
- [YamlDotNet](SixLabors/ImageSharp), MIT License
