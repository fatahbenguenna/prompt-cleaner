namespace PromptCleaner.Core.Config;

/// <summary>Résultat du chargement d'un fichier de configuration (FR-1.4 :
/// une ligne invalide n'interrompt pas le chargement, elle est comptée).</summary>
public sealed record ConfigLoadReport(
    IReadOnlyList<ReplacementRule> Rules,
    int IgnoredLineCount,
    int DuplicateKeywordCount,
    string? FilePath);
