using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SubtitlesStreamer.Domain.DTOs;

namespace SubtitlesStreamer.Application.Background;

public sealed class AudioReaderJob(
    ChannelReader<AudioDto> audioReader,
    ChannelReader<LanguageContext> languageReader,
    ChannelWriter<TranslationTask> taskWriter,
    IHttpClientFactory httpClientFactory,
    ILogger<AudioReaderJob> logger) : BackgroundService
{
    private readonly ChannelReader<AudioDto> _audioReader = audioReader;
    private readonly ChannelWriter<TranslationTask> _taskWriter = taskWriter;
    private readonly ChannelReader<LanguageContext> _languageReader = languageReader;
    private readonly HttpClient _http = httpClientFactory.CreateClient("Groq");
    private readonly ILogger<AudioReaderJob> _logger = logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var languageContext = await _languageReader.ReadAsync(stoppingToken);
        await foreach (var audio in _audioReader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var text = await TranscribeAsync(audio, languageContext.SourceLanguage, stoppingToken);

                if (string.IsNullOrWhiteSpace(text) ||
                    text.Equals(" [BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase))
                    continue;

                await _taskWriter.WriteAsync(
                    new TranslationTask(
                        Text: text,
                        LanguageContext: languageContext),
                    stoppingToken
                );
            }  
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUDIO READER] Transcription failed, skipping chunk.");
            }
        }

        _taskWriter.Complete();
    }
    
    private async Task<string?> TranscribeAsync(AudioDto audio, string language, CancellationToken token)
    {
        using var content = new MultipartFormDataContent();

        var wavBytes = BuildWav(audio.Audio, audio.SampleRate, audio.Channels);
        var fileContent = new ByteArrayContent(wavBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", "audio.wav");
        content.Add(new StringContent("whisper-large-v3"), "model");
        content.Add(new StringContent(language), "language");

        var response = await _http.PostAsync("/openai/v1/audio/transcriptions", content, token);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TranscriptionResponse>(token);
        return result?.Text;
    }

    // FFmpeg writes the WAV header once at the very beginning of the pipe with size=0 (because it doesn't know the final size). Then it streams raw PCM after that.
    // When you read fixed chunks:
    // 
    // Chunk 1 — contains the header + some PCM → valid WAV but header says size=0
    // Chunk 2+ — raw PCM with no header → faster-whisper has no idea what format it is → InvalidDataError
    // 
    // WAV was designed for files, not streams. It physically cannot work correctly when you split the pipe output into chunks.
    // -f s16le + BuildWav is the correct pattern because:
    // 
    // FFmpeg outputs pure PCM bytes with no header at all
    // Every chunk is identical raw PCM
    // You prepend a fresh correct header to each chunk before sending
    // faster-whisper gets a valid complete WAV every time
    // 
    // There's no workaround — this is a fundamental limitation of the WAV container format.
    private static byte[] BuildWav(byte[] s16LeBytes, int sampleRate, int channels)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        var dataSize = s16LeBytes.Length;
        var byteRate = sampleRate * channels * sizeof(short);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)(channels * sizeof(short)));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(s16LeBytes);

        return ms.ToArray();
    }
    
    private sealed record TranscriptionResponse(
        [property: JsonPropertyName("text")] string Text);
}