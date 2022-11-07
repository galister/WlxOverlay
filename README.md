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

# Dependencies

The following libraries are needed:
- libX11.so
- libXtst.so
- libxcb.so
- libxcb-xfixes.so
- libxcb-randr.so
- libxcb-shm.so
- libxcb-xinerama.so
- dotnet >= 6 (if building from source)
- [xshm-cap](https://github.com/galister/xshm-cap) (compiled .so is included, but you can also build it from source)

On Arch Linux:
```
pacman -S libx11 libxcb libxtst dotnet
```

# How to Build

The project uses the standard dotnet build pipeline.

```
git clone https://github.com/galister/X11Overlay.git
cd X11Overlay
dotnet build
```

Then, run with:
```
cd bin/debug/net6.0
./X11Overlay
```
(Start SteamVR before running this.)

# SteamVR bindings:
Default bindings are provided for Index Controllers. Some notes to create your own:
- `Click`: keyboard typing and clicking on the screen. set this to your triggers.
- `Grip`: for moving overlays. 
  - Recommended: `Grip` input with pressure mode, pressure 70%. Release pressure 50%
- `Pose`: set this to the controller tip. Nothing will work without this!
- `Scroll`: set this to your joystick, and choose non-discrete mode
- `ShowHide`: Show/hide the current set of overlays.
  - Recommended: DoubleClick on B / Y on your non-dominant hand
- `AltClick`: push-to-talk or arbitrary shell execute, optional. `config.yaml` to configure.
- `ClickModifierRight`: bind this to a capacitive touch action, if able
- `ClickModifierMiddle`: bind this also to a capacitive touch action
  - `ClickModifierRight` takes precedence, so this can be a button that you may accidentally touch while reaching for `ClickModifierRight`

If you can't bind the modifiers due to lack of buttons, see the `config.yaml` for alternatives.

If you're left handed, set `primary_hand: Left` in `config.yaml`.

If you do end up making bindings for a controller type that's not yet supported, please save the file using "Show Developer Output" or "Export Bindings to File" and make a pull request. Bindings on SteamVR Linux are a huge pain, let's save each other the hassle.

# Quick-Start Guide

Ensure your bindings are set up as per above!

## Pointer Modes
There are 3 modes, indicated by the color of the laser:
- Blue: left click
- Orange: right click
- Purple: middle click

These are used extensively in many different ways, so make sure you are able to access these 3 modes.

Clicking is always done by the `Click` binding, while the modified explained in SteamVR bindings change the click type.

## Juggling overlays

The screens and keyboard can be grabbed, moved and resized.

#### To show/hide:
- Use the `ShowHide` binding.

This will show `default_screen` + keyboard by default, or your last visible configuration from the current session.

#### To selectively show/hide:
- Click the buttons on the bottom of the watch

#### To reset screen/keyboard position:
- Long click (>2s) the buttons on the bottom of the watch, then release

#### To move left/right/up/down: 
- Grab the overlay using the `Grip` binding.

#### To move closer/further: 
- Ensure blue pointer mode on the grabbing hand. (Thumb should face upwards)
- Joystick forward/back while grabbing to move

#### To resize: 
- Grab the overlay
- Rotate your grabbing hand palms-upwards to get a purple pointer
- Joystick forward/back while grabbing to resize

#### To set/unset screen curvature:
- Click while the screen is being grabbed

Due to a SteamVR limitation, curved screens are fixed upright.

#### To change brightness of all overlays:
- Hover your pointer over the watch
- Joystick up/down to change brightness

## Keyboard

The default layout is my personal 60% layout, reflecting my real life setup. The layout can be changed via the `keyboard.yaml` file.

The keyboard has 3 modes. The mode comes from the color of the pointer being pointed at the keyboard.

If both pointers are on the keyboard, `primary_hand` takes precedence.

- Blue - regular keyboard
- Yellow - regular with shift
- Purple - alternative (Fn) layout

If you do end up making a layout, please submit it with a pull request, in order to so save others the trouble.

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
