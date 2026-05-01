namespace SubtitlesStreamer.Application.Services.PlaywrightService;

public interface IPlaywrightService
{
    Task InitializeAsync();
    
    Task OpenSiteAsync(string url);

    Task InitPopupAsync();
    
    Task UpdateLiveTextAsync(string text);
    
    Task CommitTextAsync(string text, int duration);
}