using System.Text.RegularExpressions;

namespace PromptCleaner.Core.Detectors;

/// <summary>D-09 : GUID/UUID. Alerte seulement : un GUID peut être un
/// identifiant public (type de document, clé de registre…) dont le
/// remplacement casserait le texte.</summary>
internal sealed class GuidDetector : IDetector
{
    public string Id => "D-09";

    private static readonly Regex Pattern = DetectorRegex.Create(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b");

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            yield return new RawDetection(match.Index, match.Length, DetectionKind.Alert, "", match.Value);
        }
    }
}

/// <summary>D-10 : secrets probables — préfixes connus (sk-, ghp_, AKIA,
/// Bearer…) ou chaîne longue à forte entropie. Alerte seulement (ADR-4) :
/// un hash git ou du base64 légitime y ressemblent trop pour remplacer à l'aveugle.</summary>
internal sealed class SecretDetector : IDetector
{
    public string Id => "D-10";

    private static readonly Regex KnownPrefixes = DetectorRegex.Create(
        @"\b(?:sk|pk)-[A-Za-z0-9_-]{16,}\b" +
        @"|\bgh[pousr]_[A-Za-z0-9]{20,}\b" +
        @"|\bgithub_pat_[A-Za-z0-9_]{20,}\b" +
        @"|\bAKIA[0-9A-Z]{16}\b" +
        @"|\bxox[bpars]-[A-Za-z0-9-]{10,}\b" +
        @"|\bBearer\s+[A-Za-z0-9._~+/=-]{16,}");

    private static readonly Regex EntropyCandidates = DetectorRegex.Create(
        @"(?<![A-Za-z0-9+/=_-])[A-Za-z0-9+/=_-]{20,}(?![A-Za-z0-9+/=_-])");

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in KnownPrefixes.Matches(text))
        {
            yield return new RawDetection(match.Index, match.Length, DetectionKind.Alert, "", match.Value);
        }

        // Les chevauchements avec les préfixes ci-dessus sont écartés par la
        // règle « premier accepté gagne » de la passe autonome.
        foreach (Match match in EntropyCandidates.Matches(text))
        {
            if (LooksLikeSecret(match.Value))
            {
                yield return new RawDetection(match.Index, match.Length, DetectionKind.Alert, "", match.Value);
            }
        }
    }

    private static bool LooksLikeSecret(string candidate)
    {
        // Majuscules + minuscules + chiffres : écarte les hash hexadécimaux
        // purs (sha1/sha256 en minuscules) et les mots ordinaires.
        bool hasUpper = false, hasLower = false, hasDigit = false;
        foreach (char c in candidate)
        {
            hasUpper |= char.IsAsciiLetterUpper(c);
            hasLower |= char.IsAsciiLetterLower(c);
            hasDigit |= char.IsAsciiDigit(c);
        }

        return hasUpper && hasLower && hasDigit && ShannonEntropy(candidate) >= 3.8;
    }

    private static double ShannonEntropy(string value)
    {
        var counts = new Dictionary<char, int>();
        foreach (char c in value)
        {
            counts[c] = counts.GetValueOrDefault(c) + 1;
        }

        double entropy = 0;
        foreach (int count in counts.Values)
        {
            double p = count / (double)value.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }
}

/// <summary>D-11 : URL dont le domaine n'est pas un site public bien connu —
/// probablement un intranet ou un service interne. Alerte seulement.</summary>
internal sealed class InternalUrlDetector : IDetector
{
    public string Id => "D-11";

    private static readonly Regex Pattern = DetectorRegex.Create(
        @"\bhttps?://[^\s""'<>()\]]+", ignoreCase: true);

    private static readonly string[] WellKnownDomains =
    [
        "github.com", "gitlab.com", "bitbucket.org",
        "stackoverflow.com", "stackexchange.com",
        "google.com", "microsoft.com", "apple.com", "mozilla.org",
        "wikipedia.org", "developer.mozilla.org",
        "nuget.org", "npmjs.com", "pypi.org", "apache.org",
        "example.com", "example.org", "example.net", "localhost",
    ];

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            if (!IsWellKnown(ExtractHost(match.Value)))
            {
                yield return new RawDetection(match.Index, match.Length, DetectionKind.Alert, "", match.Value);
            }
        }
    }

    private static string ExtractHost(string url)
    {
        int start = url.IndexOf("://", StringComparison.Ordinal) + 3;
        int end = url.IndexOfAny(['/', ':', '?', '#'], start);
        return (end < 0 ? url[start..] : url[start..end]).TrimEnd('.');
    }

    private static bool IsWellKnown(string host)
    {
        foreach (string domain in WellKnownDomains)
        {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
