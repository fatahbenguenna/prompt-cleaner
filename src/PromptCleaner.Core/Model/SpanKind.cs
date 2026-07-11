namespace PromptCleaner.Core.Model;

/// <summary>Nature d'un segment annoté du texte nettoyé.</summary>
public enum SpanKind
{
    /// <summary>Segment remplacé par le pipeline (affiché en vert).</summary>
    Replaced,

    /// <summary>Donnée suspecte détectée mais non remplacée (affichée en rouge).</summary>
    Alert,
}
