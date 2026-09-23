using System;
using System.Globalization;

namespace VibesboxKiosk.Services;

public static class ValueFormat
{
    /// <summary>Formats an OSC value with a user format string; never throws.</summary>
    public static string Apply(string format, float value)
    {
        try { return string.Format(CultureInfo.InvariantCulture, format, value); }
        catch (FormatException) { return value.ToString("0.##", CultureInfo.InvariantCulture); }
    }
}
