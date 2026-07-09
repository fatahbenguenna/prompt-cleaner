namespace PromptCleaner.Core.Model;

/// <summary>Résultat d'un nettoyage : le texte final (copié tel quel dans le
/// presse-papier), les segments annotés qui pilotent le rendu couleur et
/// d'éventuels avertissements (ex. détecteur ignoré pour cause de timeout).</summary>
public sealed record CleanResult(
    string CleanText,
    IReadOnlyList<TextSpan> Spans,
    CleanStats Stats,
    IReadOnlyList<string> Warnings);
