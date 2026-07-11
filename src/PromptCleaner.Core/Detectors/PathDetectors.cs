using System.Text.RegularExpressions;

namespace PromptCleaner.Core.Detectors;

/// <summary>D-01 : nom d'utilisateur dans un chemin Windows
/// (<c>C:\Users\jdupont\…</c> → seul « jdupont » est remplacé).</summary>
internal sealed class WindowsUserPathDetector : IDetector
{
    public string Id => "D-01";

    // Deux cas : suivi d'un « \ », le nom peut contenir des espaces
    // (C:\Users\John Doe\…) ; en fin de chemin, il s'arrête au premier espace
    // ou signe de ponctuation pour ne pas avaler la suite de la phrase.
    private static readonly Regex Pattern = DetectorRegex.Create(
        @"(?<=[A-Za-z]:\\(?:Users|Documents and Settings)\\)" +
        @"(?:[^\\/:*?""<>|\r\n]+(?=\\)|[^\\/:*?""<>|\s.,;!?)»«""']+)",
        ignoreCase: true);

    // Profils Windows génériques : les remplacer n'anonymise rien.
    private static readonly HashSet<string> GenericProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Public", "Default", "Default User", "All Users",
    };

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            // Un nom de compte Windows ne peut pas finir par un espace ou un point.
            string name = match.Value.TrimEnd(' ', '.');
            if (name.Length == 0 || GenericProfiles.Contains(name))
            {
                continue;
            }

            yield return new RawDetection(match.Index, name.Length, DetectionKind.Replace, TokenTypes.User, name);
        }
    }
}

/// <summary>D-02 : nom de serveur dans un chemin UNC
/// (<c>\\SRV-PARIS\commun</c> → seul « SRV-PARIS » est remplacé).</summary>
internal sealed class UncHostDetector : IDetector
{
    public string Id => "D-02";

    private static readonly Regex Pattern = DetectorRegex.Create(
        @"(?<=\\\\)[A-Za-z0-9][A-Za-z0-9._-]{0,62}(?=\\)");

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            if (string.Equals(match.Value, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new RawDetection(match.Index, match.Length, DetectionKind.Replace, TokenTypes.Host, match.Value);
        }
    }
}
