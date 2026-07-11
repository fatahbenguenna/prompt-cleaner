using System.Text.RegularExpressions;

namespace PromptCleaner.Core.Detectors;

/// <summary>D-06 : IBAN, validé par la somme de contrôle mod 97 — une chaîne
/// qui y ressemble mais échoue au contrôle n'est pas touchée.</summary>
internal sealed class IbanDetector : IDetector
{
    public string Id => "D-06";

    private static readonly Regex Pattern = DetectorRegex.Create(
        @"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]{2,4}){3,8}\b");

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            string compact = match.Value.Replace(" ", "");
            if (IsValidIban(compact))
            {
                yield return new RawDetection(match.Index, match.Length, DetectionKind.Replace, TokenTypes.Iban, compact);
            }
        }
    }

    private static bool IsValidIban(string iban)
    {
        if (iban.Length is < 15 or > 34)
        {
            return false;
        }

        // Contrôle mod 97 (ISO 7064) : les 4 premiers caractères passent en
        // queue, les lettres valent 10 à 35, le reste modulo 97 doit faire 1.
        string rearranged = iban[4..] + iban[..4];
        int mod = 0;
        foreach (char c in rearranged)
        {
            if (char.IsAsciiDigit(c))
            {
                mod = (mod * 10 + (c - '0')) % 97;
            }
            else if (c is >= 'A' and <= 'Z')
            {
                mod = (mod * 100 + (c - 'A' + 10)) % 97;
            }
            else
            {
                return false;
            }
        }

        return mod == 1;
    }
}

/// <summary>D-07 : NIR (numéro de sécurité sociale français, 15 chiffres),
/// validé par sa clé de contrôle.</summary>
internal sealed class NirDetector : IDetector
{
    public string Id => "D-07";

    private static readonly Regex Pattern = DetectorRegex.Create(
        @"\b[12]\s?\d{2}\s?\d{2}\s?(?:\d{2}|2[AB])\s?\d{3}\s?\d{3}\s?\d{2}\b");

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            string compact = match.Value.Replace(" ", "");
            if (HasValidKey(compact))
            {
                yield return new RawDetection(match.Index, match.Length, DetectionKind.Replace, TokenTypes.Nir, compact);
            }
        }
    }

    private static bool HasValidKey(string nir)
    {
        if (nir.Length != 15)
        {
            return false;
        }

        // Corse : 2A vaut 19 et 2B vaut 18 dans le calcul de la clé.
        string number = nir[..13]
            .Replace("2A", "19", StringComparison.OrdinalIgnoreCase)
            .Replace("2B", "18", StringComparison.OrdinalIgnoreCase);
        if (!long.TryParse(number, out long value) || !int.TryParse(nir[13..], out int key))
        {
            return false;
        }

        return key == 97 - (int)(value % 97);
    }
}

/// <summary>D-08 : numéros de carte bancaire (13 à 19 chiffres, séparateurs
/// espace/tiret tolérés), validés par l'algorithme de Luhn.</summary>
internal sealed class CreditCardDetector : IDetector
{
    public string Id => "D-08";

    private static readonly Regex Pattern = DetectorRegex.Create(
        @"(?<![\d-])\d(?:[ -]?\d){12,18}(?![\d-])");

    public IEnumerable<RawDetection> Detect(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            string digits = match.Value.Replace(" ", "").Replace("-", "");
            if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits))
            {
                yield return new RawDetection(match.Index, match.Length, DetectionKind.Replace, TokenTypes.CreditCard, digits);
            }
        }
    }

    private static bool PassesLuhn(string digits)
    {
        int sum = 0;
        bool doubleIt = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }
}
