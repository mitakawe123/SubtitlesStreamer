namespace SubtitlesStreamer.Domain.Options;

public sealed class StreamingOptions
{
    public IReadOnlyCollection<string> SupportedLanguages { get; init; } = [];
}
