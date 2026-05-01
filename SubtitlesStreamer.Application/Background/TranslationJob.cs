using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using SubtitlesStreamer.Application.Services.PlaywrightService;
using SubtitlesStreamer.Domain.DTOs;

namespace SubtitlesStreamer.Application.Background;

public sealed class TranslationJob(
    ChannelReader<TranslationTask> input,
    IHttpClientFactory httpClientFactory,
    IPlaywrightService playwright) : BackgroundService
{
    private readonly ChannelReader<TranslationTask> _input = input;
    private readonly IPlaywrightService _playwright = playwright;
    private readonly HttpClient _http = httpClientFactory.CreateClient("LibreTranslate");
    private readonly StringBuilder _textBuffer = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastCommit = Stopwatch.GetTimestamp();
        var commitInterval = TimeSpan.FromMilliseconds(20_000);

        await foreach (var task in _input.ReadAllAsync(stoppingToken))
        {
            var response = await _http.PostAsJsonAsync("/translate", new
            {
                q = task.Text,
                source = task.LanguageContext.SourceLanguage,
                target = task.LanguageContext.TargetLanguage,
                format = "text"
            }, stoppingToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LibreTranslateResponse>(stoppingToken);
            var translated = result!.TranslatedText;

            if (string.IsNullOrWhiteSpace(translated))
                continue;

            if (_textBuffer.Length > 0) 
                _textBuffer.Append(' ');
            _textBuffer.Append(translated);

            await _playwright.UpdateLiveTextAsync(_textBuffer.ToString());

            if (Stopwatch.GetElapsedTime(lastCommit) >= commitInterval)
            {
                await Commit();
                lastCommit = Stopwatch.GetTimestamp();
            }
        }

        if (_textBuffer.Length > 0)
            await Commit();
    }

    private async Task Commit()
    {
        var text = _textBuffer.ToString().Trim();
        _textBuffer.Clear();

        if (!string.IsNullOrWhiteSpace(text))
            await _playwright.CommitTextAsync(text, 300);
    }

    private sealed record LibreTranslateResponse(
        [property: JsonPropertyName("translatedText")] string TranslatedText);
}