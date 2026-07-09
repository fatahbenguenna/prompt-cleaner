using PromptCleaner.Core.Config;
using PromptCleaner.Core.Model;

namespace PromptCleaner.Core.Pipeline;

/// <summary>
/// Orchestre le nettoyage : passe 1 (dictionnaire utilisateur), puis passe 2
/// (détecteurs autonomes — itérations 3 et 4 du backlog).
/// </summary>
public sealed class CleaningPipeline
{
    /// <summary>Identifiant de « détecteur » des remplacements issus du dictionnaire.</summary>
    public const string ConfigDetectorId = "config";

    public CleanResult Clean(string text, IReadOnlyCollection<ReplacementRule> rules)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(rules);

        var segmented = new SegmentedText(text);
        DictionaryPass.Apply(segmented, rules);

        var (cleanText, spans) = segmented.Compose();
        return new CleanResult(cleanText, spans, ComputeStats(spans));
    }

    private static CleanStats ComputeStats(IReadOnlyList<TextSpan> spans)
    {
        int config = 0;
        int auto = 0;
        int alerts = 0;
        foreach (var span in spans)
        {
            if (span.Kind == SpanKind.Alert)
            {
                alerts++;
            }
            else if (span.DetectorId == ConfigDetectorId)
            {
                config++;
            }
            else
            {
                auto++;
            }
        }

        return new CleanStats(config, auto, alerts);
    }
}
