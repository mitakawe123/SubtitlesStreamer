using System.Buffers;
using System.Diagnostics;
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

    private readonly byte[] _byteBuffer = ArrayPool<byte>.Shared.Rent(ChunkBytes);
    private readonly float[] _floatBuffer = ArrayPool<float>.Shared.Rent(ChunkBytes / 4);

    private readonly ChannelWriter<AudioDto> _audioWriter = audioWriter;
    private readonly Stream _stream = ffmpegProcessorService.InitBaseStream();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _stream.ReadExactlyAsync(_byteBuffer.AsMemory(0, ChunkBytes), stoppingToken);

                    var floatSpan = MemoryMarshal.Cast<byte, float>(_byteBuffer.AsSpan(0, ChunkBytes));
                    floatSpan.CopyTo(_floatBuffer.AsSpan(0, floatSpan.Length));

                    await _audioWriter.WriteAsync(new AudioDto(_floatBuffer), stoppingToken);
                }
                catch (EndOfStreamException)
                {
                    break; // Stream ended
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("[ERROR WRITER] {ex}", ex);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(_byteBuffer,true);
            ArrayPool<float>.Shared.Return(_floatBuffer,true);
        }    
    }
}