using System.Diagnostics;

namespace SubtitlesStreamer.Application.Services.FfmpegProcessor;

public class FfmpegProcessorService : IFfmpegProcessorService, IDisposable
{
    private Process? _ffmpeg;
    
    public Stream InitBaseStream()
    {
        var monitorDevice = GetDefaultPulseMonitorDevice();
        var ffmpegArgs = $"-f pulse -i {monitorDevice} -ac 1 -ar 16000 -f s16le -";
        
        var ffmpegInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = ffmpegArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        _ffmpeg = Process.Start(ffmpegInfo)
                  ?? throw new InvalidOperationException("Failed to start ffmpeg");
        
        _ = Task.Run(() => _ffmpeg.StandardError.ReadToEndAsync());

        return _ffmpeg.StandardOutput.BaseStream;
    }

    private static string GetDefaultPulseMonitorDevice()
    {
        // Step 1: get the name of the default sink (output device)
        var defaultSink = RunCommand("pactl", "get-default-sink").Trim();

        if (string.IsNullOrEmpty(defaultSink))
            throw new InvalidOperationException("Could not determine default PulseAudio sink.");

        // Step 2: the monitor device is always just "<sink-name>.monitor"
        return $"{defaultSink}.monitor";
    }

    private static string RunCommand(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi) 
                            ?? throw new InvalidOperationException($"Failed to start {fileName}");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    
    public void Dispose()
    {
        if (_ffmpeg is null) 
            return;
        
        _ffmpeg.Dispose();
        _ffmpeg = null;
    }
}