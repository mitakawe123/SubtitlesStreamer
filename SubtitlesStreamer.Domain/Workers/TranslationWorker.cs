using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using SubtitlesStreamer.Domain.DTOs;

namespace SubtitlesStreamer.Domain.Workers;

public sealed class TranslationWorker(
    ChannelReader<TranslationTask> input,
    ChannelWriter<TranslationResult> output,
    IHttpClientFactory httpClientFactory) : IDisposable
{
    private readonly ChannelReader<TranslationTask> _input = input;
    private readonly ChannelWriter<TranslationResult> _output = output;
    private readonly HttpClient _http = httpClientFactory.CreateClient("LibreTranslate");
    
    public async Task RunAsync(CancellationToken token)
    {
        await foreach (var task in _input.ReadAllAsync(token))
        {
            var translated = await TranslateAsync(task, token);
            await _output.WriteAsync(new TranslationResult(task.SequenceId, translated), token);
        }
    }

    private async Task<string> TranslateAsync(TranslationTask task, CancellationToken token)
    {
        var response = await _http.PostAsJsonAsync("/translate", new
        {
            q = task.Text,
            source = task.LanguageContext.SourceLanguage,
            target = task.LanguageContext.TargetLanguage,
            format = "text"
        }, token);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LibreTranslateResponse>(token);
        return result!.TranslatedText;
    }

    public void Dispose() => _http.Dispose();
    
    private sealed record LibreTranslateResponse(
        [property: JsonPropertyName("translatedText")] string TranslatedText);
}