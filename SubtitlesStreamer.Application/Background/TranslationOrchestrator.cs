using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using SubtitlesStreamer.Domain.DTOs;
using SubtitlesStreamer.Domain.Workers;

namespace SubtitlesStreamer.Application.Background;

public sealed class TranslationOrchestrator(
    ChannelReader<TranslationTask> taskReader,
    ChannelWriter<TranslationResult> resultWriter) : BackgroundService
{
    private const int WorkerCount = 3;
    
    private readonly ChannelReader<TranslationTask> _taskReader = taskReader;
    private readonly ChannelWriter<TranslationResult> _resultWriter = resultWriter;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, WorkerCount)
            .Select(_ =>
            {
                var worker = new TranslationWorker(_taskReader, _resultWriter);
                return Task.Run(() => worker.RunAsync(stoppingToken), stoppingToken);
            });

        await Task.WhenAll(workers);

        _resultWriter.Complete();
    }
}