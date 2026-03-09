using System.Threading.Channels;
using BergamotTranslatorSharp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SubtitlesStreamer.Application.Services.PlaywrightService;
using SubtitlesStreamer.Domain.DTOs;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;

namespace SubtitlesStreamer.Application.Background;

public sealed class AudioReaderJob(
    ChannelReader<AudioDto> audioReader,
    ChannelReader<LanguageContext> languageReader,
    ILogger<AudioReaderJob> logger,
    IPlaywrightService playwrightService) : BackgroundService
{
    private const string LanguageModelsFolder = "models";
    private const string ModelFileName = "ggml-base.bin";

    private readonly string _modelsPath = Path.Combine(AppContext.BaseDirectory, LanguageModelsFolder);
    private readonly string _factoryPath = Path.Combine(AppContext.BaseDirectory, ModelFileName);

    private readonly ChannelReader<AudioDto> _audioReader = audioReader;
    private readonly ChannelReader<LanguageContext> _languageReader = languageReader;
    private readonly ILogger<AudioReaderJob> _logger = logger;
    private readonly IPlaywrightService _playwrightService = playwrightService;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var languageContext in _languageReader.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (!File.Exists(_factoryPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_factoryPath)!);
                    await DownloadModel(_factoryPath);
                }

                using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);
                using var factory = WhisperFactory.FromPath(_factoryPath);                          
                await using var processor = factory.CreateBuilder()
                    .WithLanguage(languageContext.SourceLanguage)
                    .Build();

                var modelPath = Path.Combine(_modelsPath,
                    $"{languageContext.SourceLanguage}-{languageContext.TargetLanguage}/{languageContext.SourceLanguage}-{languageContext.TargetLanguage}.yml");
                using var translateService = new BlockingService(modelPath);

                await foreach (var floatChunk in _audioReader.ReadAllAsync(stoppingToken))
                {
                    await foreach (var segment in processor.ProcessAsync(floatChunk.Audio, stoppingToken))
                    {
                        if (string.IsNullOrWhiteSpace(segment.Text) || segment.Text is " [BLANK_AUDIO]")
                            continue;

                        var translatedResult = translateService.Translate(segment.Text);
                        await _playwrightService.ShowTranslatePopupTextAsync(translatedResult);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation("[ERROR READER] {ex}", ex);
            }
        }
    }
    
    private static async Task DownloadModel(string filePath)
    {
        Console.WriteLine($"Downloading Model {filePath}");
        await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Base);
        await using var fileWriter = File.OpenWrite(filePath);
        await modelStream.CopyToAsync(fileWriter);
    }
}