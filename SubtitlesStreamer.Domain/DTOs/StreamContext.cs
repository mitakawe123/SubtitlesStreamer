namespace SubtitlesStreamer.Domain.DTOs;

public sealed record StreamContext(
    string Url,
    string SourceLanguage,
    string TargetLanguage) : LanguageContext(SourceLanguage, TargetLanguage);