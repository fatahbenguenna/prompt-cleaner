namespace PromptCleaner.Core.Config;

/// <summary>Règle du dictionnaire : toute occurrence de <paramref name="Keyword"/>
/// (insensible à la casse) est remplacée littéralement par <paramref name="Replacement"/>.</summary>
public sealed record ReplacementRule(string Keyword, string Replacement);
