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
    private const int ChunkBytes = 32768; // 32 KB
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 2000;

    private readonly ChannelWriter<AudioDto> _audioWriter = audioWriter;
    private readonly ILogger<AudioWriterJob> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var byteBuffer = ArrayPool<byte>.Shared.Rent(ChunkBytes);
            var floatBuffer = ArrayPool<float>.Shared.Rent(ChunkBytes / 4);

            try
            {
                await using var stream = ffmpegProcessorService.InitBaseStream();
                attempt = 0; // reset on successful stream init

                while (!stoppingToken.IsCancellationRequested)
                {
                    await stream.ReadExactlyAsync(byteBuffer.AsMemory(0, ChunkBytes), stoppingToken);
                    var floatSpan = MemoryMarshal.Cast<byte, float>(byteBuffer.AsSpan(0, ChunkBytes));
                    floatSpan.CopyTo(floatBuffer.AsSpan(0, floatSpan.Length));
                    await _audioWriter.WriteAsync(new AudioDto(floatBuffer), stoppingToken);
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
                ArrayPool<float>.Shared.Return(floatBuffer, true);
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