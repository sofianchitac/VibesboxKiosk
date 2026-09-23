# REAPER example

One way to drive the kiosk from REAPER, using the
[ReaLearn](https://www.helgoboss.org/projects/realearn) plugin for the OSC
mappings. The addresses match the kiosk's default settings.

| File | What it does |
|---|---|
| `Vibesbox_Spectrum.jsfx` | 64-band spectrum analyser (10 Hz–20 kHz, dB). It also reports sample rate, buffer size and latency. |
| `spectrum-realearn.json` | ReaLearn preset that sends the analyser's bands to `/vibesbox/spectrum/0…63`. |
| `realearn-kiosk-osc.json` | Buttons `/vibesbox/01…08`, the status row, the refresh request and the quit-before-shutdown action. |
| `realearn-kiosk-midi.json` | Volume, bass and treble feedback for the value pop-ups, plus the MIDI activity flag. For a hardware MIDI controller. |

## Setup

1. In ReaLearn, add an OSC device. Set **Local port** to the kiosk's *send
   port* (8000), **Device host** to `127.0.0.1`, and **Device port** to the
   kiosk's *listen port* (9000). Use this device for both input and output.
2. Put `Vibesbox_Spectrum.jsfx` on the track you want to see, followed by a
   ReaLearn instance with `spectrum-realearn.json`.
3. Import the other two presets into a ReaLearn instance on a control track.
   They are **templates**: point each mapping at your own tracks, FX and
   actions.

The two ports are easy to swap by mistake. If buttons do nothing, check them
first.
