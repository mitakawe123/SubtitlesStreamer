using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using SubtitlesStreamer.Application.Services.PlaywrightService;
using SubtitlesStreamer.Domain.DTOs;

namespace SubtitlesStreamer.Application.Background;

public sealed class StreamInitializerJob(
    ChannelReader<StreamContext> reader,
    ChannelWriter<LanguageContext> writer,
    IPlaywrightService playwrightService) : BackgroundService
{
    private readonly ChannelReader<StreamContext> _reader = reader;
    private readonly ChannelWriter<LanguageContext> _writer = writer;
    private readonly IPlaywrightService _playwrightService = playwrightService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach(var streamContext in _reader.ReadAllAsync(stoppingToken))
        {
            await _playwrightService.InitializeAsync();
            await _playwrightService.OpenSiteAsync(streamContext.Url);

            await _writer.WriteAsync(new LanguageContext(streamContext.SourceLanguage, streamContext.TargetLanguage), stoppingToken);
        }
    }
}