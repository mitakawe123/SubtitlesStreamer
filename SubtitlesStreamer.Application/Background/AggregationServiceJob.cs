using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using SubtitlesStreamer.Application.Services.PlaywrightService;
using SubtitlesStreamer.Domain.DTOs;

namespace SubtitlesStreamer.Application.Background;

public sealed class AggregationServiceJob(
    ChannelReader<TranslationResult> reader,
    IPlaywrightService playwright): BackgroundService
{
    private readonly ChannelReader<TranslationResult> _reader = reader;
    private readonly IPlaywrightService _playwright = playwright;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var buffer = new SortedDictionary<long, string>();
        var textBuffer = new StringBuilder();

        long expected = 0;

        var lastRendered = string.Empty;

        var liveInterval = TimeSpan.FromMilliseconds(50);
        var commitInterval = TimeSpan.FromMilliseconds(10_000);

        var lastLive = DateTime.UtcNow;
        var lastCommit = DateTime.UtcNow;

        await foreach (var result in _reader.ReadAllAsync(stoppingToken))
        {
            buffer[result.SequenceId] = result.TranslatedText;

            while (buffer.TryGetValue(expected, out var text))
            {
                textBuffer.Append(text).Append(' ');
                buffer.Remove(expected);
                expected++;
            }

            var now = DateTime.UtcNow;

            if (now - lastLive >= liveInterval)
            {
                var partial = textBuffer.ToString().Trim();

                if (!string.IsNullOrWhiteSpace(partial) && partial != lastRendered)
                {
                    await _playwright.UpdateLiveTextAsync(partial);
                    lastRendered = partial;
                }

                lastLive = now;
            }

            if (now - lastCommit < commitInterval) 
                continue;
            
            await Commit(textBuffer);

            lastCommit = now;
            lastLive = now;
            lastRendered = string.Empty;
        }

        if (textBuffer.Length > 0)
            await Commit(textBuffer);
    }
    private async Task Commit(StringBuilder buffer)
    {
        var text = buffer.ToString().Trim();
        buffer.Clear();

        if (string.IsNullOrWhiteSpace(text))
            return;

        await _playwright.CommitTextAsync(text);
    }
}