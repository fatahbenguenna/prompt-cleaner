using System.Text.RegularExpressions;

namespace PromptCleaner.Core.Detectors;

/// <summary>D-03 : adresses e-mail.</summary>
internal sealed class EmailDetector : IDetector
{
    public string Id => "D-03";

    private static readonly Regex Pattern = DetectorRegex.Create(
        @"(?<![A-Za-z0-9._%+-])[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b");

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            yield return new RawDetection(match.Index, match.Length, DetectionKind.Replace, TokenTypes.Email, match.Value);
        }
    }
}

/// <summary>D-04 : adresses IPv4, hors localhost et plages de documentation
/// (RFC 5737) pour limiter les faux positifs.</summary>
internal sealed class IpV4Detector : IDetector
{
    public string Id => "D-04";

    private static readonly Regex Pattern = DetectorRegex.Create(
        @"(?<![\d.])(?:(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)(?!\.?\d)");

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            if (!IsExcluded(match.Value))
            {
                yield return new RawDetection(match.Index, match.Length, DetectionKind.Replace, TokenTypes.Ip, match.Value);
            }
        }
    }

    private static bool IsExcluded(string ip)
    {
        string[] octets = ip.Split('.');
        return octets[0] == "127"
            || ip == "0.0.0.0"
            || ip == "255.255.255.255"
            || ip.StartsWith("192.0.2.", StringComparison.Ordinal)
            || ip.StartsWith("198.51.100.", StringComparison.Ordinal)
            || ip.StartsWith("203.0.113.", StringComparison.Ordinal);
    }
}

/// <summary>D-05 : numéros de téléphone français (0x xx xx xx xx, +33…),
/// séparateurs espace, point ou tiret tolérés.</summary>
internal sealed class FrenchPhoneDetector : IDetector
{
    public string Id => "D-05";

    private static readonly Regex Pattern = DetectorRegex.Create(
        @"(?<!\d)(?:\+33[\s.-]?|0)[1-9](?:[\s.-]?\d{2}){4}(?!\d)");

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            yield return new RawDetection(match.Index, match.Length, DetectionKind.Replace, TokenTypes.Phone, match.Value);
        }
    }
}
