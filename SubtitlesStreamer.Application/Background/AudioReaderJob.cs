using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using SubtitlesStreamer.Domain.DTOs;
using Whisper.net;

namespace SubtitlesStreamer.Application.Background;

public sealed class AudioReaderJob(
    ChannelReader<AudioDto> audioReader,
    ChannelReader<LanguageContext> languageReader,
    ChannelWriter<TranslationTask> taskWriter) : BackgroundService
{
    private const string ModelFileName = "ggml-small.bin";
    private readonly string _factoryPath = Path.Combine(AppContext.BaseDirectory, ModelFileName);

    private readonly ChannelReader<AudioDto> _audioReader = audioReader;
    private readonly ChannelWriter<TranslationTask> _taskWriter = taskWriter;
    private readonly ChannelReader<LanguageContext> _languageReader = languageReader;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ulong seq = 0;

        await foreach (var languageContext in _languageReader.ReadAllAsync(stoppingToken))
        {
            using var factory = WhisperFactory.FromPath(_factoryPath);
            await using var processor = factory.CreateBuilder()
                .WithLanguage(languageContext.SourceLanguage)
                .Build();

            await foreach (var audio in _audioReader.ReadAllAsync(stoppingToken))
            {
                await foreach (var segment in processor.ProcessAsync(audio.Audio, stoppingToken))
                {
                    if (string.IsNullOrWhiteSpace(segment.Text) || segment.Text.Equals(" [BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase))
                        continue;

                    await _taskWriter.WriteAsync(
                        new TranslationTask(
                            SequenceId: seq++, 
                            Text: segment.Text,
                            LanguageContext: languageContext),
                        stoppingToken
                    );
                }
            }
        }

        _taskWriter.Complete();
    }
}