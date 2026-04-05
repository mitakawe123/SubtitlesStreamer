namespace SubtitlesStreamer.Application.Services.PlaywrightService;

public interface IPlaywrightService
{
    Task OpenSiteAsync(string url);

    Task ClickConsentButtonAsync();

    Task ShowTranslatePopupTextAsync(string translatedResult);
}