namespace PromptCleaner.Core.Model;

/// <summary>Résultat d'un nettoyage : le texte final (copié tel quel dans le
/// presse-papier) et les segments annotés qui pilotent le rendu couleur.</summary>
public sealed record CleanResult(string CleanText, IReadOnlyList<TextSpan> Spans, CleanStats Stats);
