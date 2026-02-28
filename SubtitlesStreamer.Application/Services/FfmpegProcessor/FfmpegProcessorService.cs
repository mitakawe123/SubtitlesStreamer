using System.Diagnostics;

namespace SubtitlesStreamer.Application.Services.FfmpegProcessor;

public class FfmpegProcessorService : IFfmpegProcessorService
{
    public Stream InitBaseStream()
    {
        const string pulseMonitorDevice = "alsa_output.pci-0000_00_1f.3.analog-stereo.monitor"; // need to think of a way to dynamically load this because now with headphones or on windows it will not work 
        const string ffmpegArgs = $"-f pulse -i {pulseMonitorDevice} -ac 1 -ar 16000 -f f32le -";
        var ffmpegInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = ffmpegArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        var ffmpeg = Process.Start(ffmpegInfo) 
                     ?? throw new NullReferenceException("Failed to start ffmpeg");
        
        _ = Task.Run(() => ffmpeg.StandardError.ReadToEnd());

        return ffmpeg.StandardOutput.BaseStream;
    }
}