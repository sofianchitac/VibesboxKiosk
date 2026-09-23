namespace VibesboxKiosk.Services;

/// <summary>Stops every running service before the dashboard is rebuilt or the settings page opens.</summary>
public static class KioskServices
{
    public static void StopAll()
    {
        ButtonBindingService.Instance.Clear();
        AutomationService.Stop();
        NowPlayingService.Instance.Stop();
        OverlayService.Instance.Stop();
        IndicatorService.Instance.Stop();
        SpectrumService.Instance.Stop();
        OscService.Instance.Stop();
    }
}
