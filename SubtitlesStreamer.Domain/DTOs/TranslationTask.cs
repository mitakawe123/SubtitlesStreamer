namespace SubtitlesStreamer.Domain.DTOs;

public sealed record TranslationTask(
    string Text,
    LanguageContext LanguageContext
);