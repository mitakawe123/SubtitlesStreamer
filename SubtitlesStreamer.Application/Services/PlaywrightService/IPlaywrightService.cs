namespace SubtitlesStreamer.Application.Services.PlaywrightService;

public interface IPlaywrightService
{
    Task InitializeAsync();
    
    Task OpenSiteAsync(string url);

    Task ShowTranslatePopupTextAsync(string translatedResult);
}