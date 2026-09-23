using System.Collections.Generic;

namespace VibesboxKiosk.Config;

/// <summary>The configuration written on first run: a working example layout.</summary>
public static class DefaultConfig
{
    public static KioskConfig Create()
    {
        var cfg = new KioskConfig();

        cfg.Stats.AddRange(new[]
        {
            new StatConfig { Label = "DSP", Address = "/vibesbox/dsp/on", OnOff = true, TimeoutMs = 1500 },
            new StatConfig { Label = "LATENCY", Address = "/vibesbox/dsp/latency", Unit = "ms" },
            new StatConfig { Label = "SAMPLE RATE", Address = "/vibesbox/dsp/clock", Unit = "kHz" },
            new StatConfig { Label = "BUFFER", Address = "/vibesbox/dsp/buffer" },
        });

        string[] names = { "Upmix", "Room EQ", "Loudness", "Night", "Front", "Centre", "Surround", "Subwoofer" };
        for (int i = 0; i < names.Length; i++)
        {
            string n = (i + 1).ToString("00");
            cfg.Buttons.Items.Add(new ActionButtonConfig
            {
                Id = $"btn-{n}",
                Name = names[i],
                OscOut = $"/vibesbox/{n}",
                OscFeedback = $"/vibesbox/{n}",
                States = new List<ButtonStateConfig>
                {
                    new() { Name = "off", Label = "OFF" },
                    new() { Name = "playing", Label = "ON" },
                },
            });
        }

        cfg.Overlays.AddRange(new[]
        {
            new OverlayConfig { Id = "vol", Label = "VOLUME", OscFeedback = "/vibesbox/master/db", Format = "{0:0}" },
            new OverlayConfig { Id = "bass", Label = "BASS", OscFeedback = "/vibesbox/eq/bass", Format = "{0:+0;-0;0} dB" },
            new OverlayConfig { Id = "treble", Label = "TREBLE", OscFeedback = "/vibesbox/eq/treble", Format = "{0:+0;-0;0} dB" },
        });

        cfg.Activity.AddRange(new[]
        {
            new ActivityConfig { Label = "MIDI", Address = "/vibesbox/in/midi/active" },
            new ActivityConfig { Label = "OSC", Address = ActivityConfig.OscOut },
        });

        return cfg;
    }
}
