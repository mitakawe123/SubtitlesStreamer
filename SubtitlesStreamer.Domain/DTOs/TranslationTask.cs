namespace SubtitlesStreamer.Domain.DTOs;

public sealed record TranslationTask(
    long SequenceId,
    string Text,
    LanguageContext LanguageContext
);