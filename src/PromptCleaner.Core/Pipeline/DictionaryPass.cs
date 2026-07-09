using PromptCleaner.Core.Config;

namespace PromptCleaner.Core.Pipeline;

/// <summary>Passe 1 : remplacement de toutes les occurrences des mots-clés du
/// dictionnaire utilisateur. Les mots-clés longs passent en premier pour
/// qu'un mot-clé court n'ampute pas un mot-clé qui le contient (FR-3.2).</summary>
internal static class DictionaryPass
{
    public static void Apply(SegmentedText text, IReadOnlyCollection<ReplacementRule> rules)
    {
        var ordered = rules
            .Where(r => r.Keyword.Length > 0)
            .OrderByDescending(r => r.Keyword.Length)
            .ThenBy(r => r.Keyword, StringComparer.OrdinalIgnoreCase);

        foreach (var rule in ordered)
        {
            text.ReplaceLiteral(rule.Keyword, rule.Replacement, CleaningPipeline.ConfigDetectorId);
        }
    }
}
