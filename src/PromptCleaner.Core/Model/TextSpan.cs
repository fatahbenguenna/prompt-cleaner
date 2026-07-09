namespace PromptCleaner.Core.Model;

/// <summary>
/// Segment annoté du texte nettoyé. <paramref name="Start"/> et <paramref name="Length"/>
/// sont exprimés en unités UTF-16 dans <see cref="CleanResult.CleanText"/>.
/// </summary>
/// <param name="Start">Position du segment dans le texte final.</param>
/// <param name="Length">Longueur du segment dans le texte final.</param>
/// <param name="Kind">Remplacement effectué ou simple alerte.</param>
/// <param name="DetectorId">Origine : "config" pour le dictionnaire, sinon l'identifiant du détecteur (D-01…).</param>
/// <param name="Original">Texte d'origine couvert par le segment.</param>
public sealed record TextSpan(int Start, int Length, SpanKind Kind, string DetectorId, string Original);
