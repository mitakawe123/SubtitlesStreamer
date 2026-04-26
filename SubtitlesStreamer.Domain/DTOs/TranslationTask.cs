namespace SubtitlesStreamer.Domain.DTOs;

public sealed record TranslationTask(
    ulong SequenceId,
    string Text,
    LanguageContext LanguageContext
);