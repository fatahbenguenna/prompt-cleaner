using System.Text.RegularExpressions;

namespace PromptCleaner.Core.Detectors;

internal enum DetectionKind
{
    /// <summary>Remplacement sûr : la valeur devient un jeton XX_TYPE_XX (vert).</summary>
    Replace,

    /// <summary>Donnée suspecte laissée intacte mais signalée (rouge).</summary>
    Alert,
}

/// <summary>Détection brute, en positions relatives au texte du segment analysé.
/// <paramref name="Value"/> est la valeur sensible, utilisée pour attribuer un
/// jeton stable (même valeur ⇒ même jeton, FR-4.2).</summary>
internal sealed record RawDetection(int Start, int Length, DetectionKind Kind, string TokenType, string Value);

/// <summary>Détecteur de la passe autonome (D-01 à D-11 ; D-12 est orchestré à
/// part car il dépend des valeurs relevées par D-01).</summary>
internal interface IDetector
{
    string Id { get; }

    IEnumerable<RawDetection> Detect(string text);
}

/// <summary>Types de jeton produits par les détecteurs (XX_USER_XX, XX_EMAIL_XX…).</summary>
internal static class TokenTypes
{
    public const string User = "USER";
    public const string Host = "HOST";
    public const string Email = "EMAIL";
    public const string Ip = "IP";
    public const string Phone = "TEL";
    public const string Iban = "IBAN";
    public const string Nir = "NIR";
    public const string CreditCard = "CB";
}

internal static class DetectorRegex
{
    /// <summary>Toutes les regex des détecteurs sont compilées et bornées dans
    /// le temps : un texte pathologique ne doit jamais geler l'application (NFR-5).</summary>
    public static Regex Create(string pattern, bool ignoreCase = false)
    {
        var options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        if (ignoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new Regex(pattern, options, TimeSpan.FromSeconds(1));
    }
}
