using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SubtitlesStreamer.Application.Services.FfmpegProcessor;
using SubtitlesStreamer.Domain.DTOs;

namespace SubtitlesStreamer.Application.Background;

public sealed class AudioWriterJob(
    ChannelWriter<AudioDto> audioWriter,
    IFfmpegProcessorService ffmpegProcessorService,
    ILogger<AudioWriterJob> logger) : BackgroundService
{
    // Whisper expects 16kHz mono float32
    // 0.5s * 16000 samples * 4 bytes = 32000 bytes — be explicit
    private const int SampleRate = 16000;
    private const float ChunkDurationSeconds = 0.5f;
    private const int SamplesPerChunk = (int)(SampleRate * ChunkDurationSeconds);
    private const int ChunkBytes = SamplesPerChunk * sizeof(float);
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    private readonly ChannelWriter<AudioDto> _audioWriter = audioWriter;
    private readonly IFfmpegProcessorService _ffmpegProcessorService = ffmpegProcessorService;
    private readonly ILogger<AudioWriterJob> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;
        await using var stream = _ffmpegProcessorService.InitBaseStream();

        while (!stoppingToken.IsCancellationRequested)
        {
            var byteBuffer = ArrayPool<byte>.Shared.Rent(ChunkBytes);

            try
            {
                attempt = 0; // reset on successful stream init

                while (!stoppingToken.IsCancellationRequested)
                {
                    await stream.ReadExactlyAsync(byteBuffer.AsMemory(0, ChunkBytes), stoppingToken);
                    
                    var floats = new float[SamplesPerChunk];
                    MemoryMarshal.Cast<byte, float>(byteBuffer.AsSpan(0, ChunkBytes))
                        .CopyTo(floats);
                    
                    await _audioWriter.WriteAsync(new AudioDto(floats), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[AUDIO WRITER] Graceful shutdown requested, stopping.");
                break;
            }
            catch (EndOfStreamException)
            {
                attempt++;
                _logger.LogWarning("[AUDIO WRITER] Stream ended unexpectedly. Retry {attempt}/{MaxRetries}...", attempt, MaxRetries);
            }
            catch (Exception ex)
            {
                attempt++;
                _logger.LogError(ex, "[AUDIO WRITER] Unexpected error. Retry {attempt}/{MaxRetries}...", attempt, MaxRetries);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(byteBuffer, true);
            }

            if (attempt >= MaxRetries)
            {
                _logger.LogCritical("[AUDIO WRITER] Max retries ({MaxRetries}) reached. Giving up.", MaxRetries);
                break;
            }

            await Task.Delay(RetryDelayMs, stoppingToken);
        }
    }
}