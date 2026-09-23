# Vibesbox Kiosk

A full-screen touch dashboard for a Windows PC running audio DSP in a DAW or
plugin host, such as REAPER with room correction and upmixing plugins. It turns
that PC into an appliance: a live spectrum, status values, big buttons for the
things you switch often, and power controls. No Windows desktop in sight.

The kiosk talks to the DSP host over **OSC**. It doesn't care which host that
is: anything that can send and receive OSC works. Part of the
[Vibesbox](https://github.com/sofianchitac/Vibesbox) home audio system, but it
runs on its own.

## What's on screen

- **Spectrum**: one OSC value per band, drawn as a smooth curve.
- **Status row**: any values you like, e.g. sample rate, latency or DSP on/off.
- **Buttons**: toggle, momentary or one-shot. Each sends an OSC value and
  follows the host's feedback, so it never shows a stale state.
- **Value pop-ups**: a large readout when volume, bass or another value changes.
- **Indicators**: input level dots with group labels, plus activity dots (MIDI
  in, OSC out…).
- **Logo toggle**: tap the logo to switch a mode on and off, with an optional
  pulsing edge glow.
- **Now Playing** (optional): track, artist and artwork from a WebSocket feed
  ([protocol](docs/now-playing.md)).
- **Automations** (optional): when a WebSocket feed reports a change, send an
  OSC value. For example: upmixing off when the source turns multichannel.

The layout scales to any screen size and orientation. Colours and the logo can
be changed.

## Install

1. Download `VibesboxKiosk-Setup-<version>.exe` from
   [Releases](https://github.com/sofianchitac/VibesboxKiosk/releases) and run
   it. It needs Windows 10 1809 or later (64-bit), and nothing else: the
   runtime is included.
2. Choose whether the kiosk starts when you sign in.

The installer isn't code-signed, so SmartScreen may warn on first run. Choose
*More info → Run anyway*.

## Set up

**Press and hold the logo** (or right-click it, or press `Ctrl+,`) to open the
settings. Every option is there, from OSC ports to button names to colours.
Save applies the changes at once.

The settings are stored in `%LOCALAPPDATA%\VibesboxKiosk\kiosk-config.json`.
Place a `kiosk-config.json` next to `VibesboxKiosk.exe` instead to keep the
install self-contained. Hand edits to the file apply live too.

On the host side, map the kiosk's OSC addresses to your tracks, plugins and
actions. [`examples/reaper`](examples/reaper) has a spectrum analyser and
ReaLearn presets that match the default settings.

## Running as an appliance

- **Always on top** keeps the DAW from covering the kiosk. Use the settings'
  *Quit kiosk* button, or Alt+F4 from a keyboard, to get back to the desktop.
- The power button offers shutdown, restart and display off. It can send an
  OSC message first, so the DAW saves and quits cleanly.
- The installer's autostart uses the standard Run key. A machine with a custom
  shell (in place of Explorer) skips that. Start the kiosk from an *At log on*
  Scheduled Task instead.

## Build from source

Requires Windows, the .NET 10 SDK and the C++ build tools (for Native AOT).

```powershell
.\build\publish.ps1                                              # → publish\VibesboxKiosk.exe
iscc /DAppVersion=1.0.0 installer\VibesboxKiosk.iss              # optional: Inno Setup installer
```

For development, `dotnet run --project src\VibesboxKiosk` runs a debug build.
Set *Full screen* and *Always on top* off to work next to it. Logs go to
`%LOCALAPPDATA%\VibesboxKiosk\logs`.

## Licence

[MIT](LICENSE). Instrument Sans is used under the
[SIL Open Font License](src/VibesboxKiosk/Themes/Fonts/OFL.txt).
