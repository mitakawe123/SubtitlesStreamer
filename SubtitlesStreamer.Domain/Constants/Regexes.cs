using System.Text.RegularExpressions;

namespace SubtitlesStreamer.Domain.Constants;

public static partial class Regexes
{
    [GeneratedRegex("accept|agree|Давам Съгласие", RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex ConsentButton { get; }
}
