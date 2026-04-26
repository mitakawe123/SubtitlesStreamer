using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using SubtitlesStreamer.Domain.DTOs;
using SubtitlesStreamer.Domain.Workers;

namespace SubtitlesStreamer.Application.Background;

public sealed class TranslationOrchestrator(
    ChannelReader<TranslationTask> taskReader,
    ChannelWriter<TranslationResult> resultWriter,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private const int WorkerCount = 5;
    
    private readonly ChannelReader<TranslationTask> _taskReader = taskReader;
    private readonly ChannelWriter<TranslationResult> _resultWriter = resultWriter;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, WorkerCount)
            .Select(_ =>
            {
                var worker = new TranslationWorker(_taskReader, _resultWriter, _httpClientFactory);
                return Task.Run(() => worker.RunAsync(stoppingToken), stoppingToken);
            });

        await Task.WhenAll(workers);

        _resultWriter.Complete();
    }
}