using System.Threading.Channels;
using BergamotTranslatorSharp;
using SubtitlesStreamer.Domain.DTOs;

namespace SubtitlesStreamer.Domain.Workers;

public sealed class TranslationWorker(
    ChannelReader<TranslationTask> input,
    ChannelWriter<TranslationResult> output)
{
    private readonly ChannelReader<TranslationTask> _input = input;
    private readonly ChannelWriter<TranslationResult> _output = output;
    
    private readonly Dictionary<string, BlockingService> _translators = new();

    public async Task RunAsync(CancellationToken token)
    {
        await foreach (var task in _input.ReadAllAsync(token))
        {
            var translator = GetOrCreateTranslator(task.LanguageContext);
            var translated = translator.Translate(task.Text);

            await _output.WriteAsync(
                new TranslationResult(task.SequenceId, translated),
                token
            );
        }
    }

    private BlockingService GetOrCreateTranslator(LanguageContext ctx)
    {
        var key = $"{ctx.SourceLanguage}-{ctx.TargetLanguage}";

        if (_translators.TryGetValue(key, out var existing))
            return existing;

        var modelPath = Path.Combine(
            AppContext.BaseDirectory,
            "models",
            key,
            $"{key}.yml"
        );

        var service = new BlockingService(modelPath);
        _translators[key] = service;

        return service;
    }
}