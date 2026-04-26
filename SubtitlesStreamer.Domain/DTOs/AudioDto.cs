namespace SubtitlesStreamer.Domain.DTOs;

public sealed record AudioDto(byte[] Audio, int SampleRate = 16000, int Channels = 1);
