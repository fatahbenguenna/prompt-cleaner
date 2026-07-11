using System.Text.RegularExpressions;
using PromptCleaner.Core.Detectors;

namespace PromptCleaner.Core.Pipeline;

/// <summary>
/// Passe 2 : détection autonome des données sensibles restantes (FR-4).
/// Les détecteurs s'exécutent dans l'ordre D-01 → D-12 sur les segments encore
/// libres ; en cas de chevauchement, la première détection acceptée gagne.
/// Les détections sont toutes collectées avant application, ce qui permet
/// d'attribuer des jetons cohérents sur l'ensemble du texte (FR-4.1/4.2).
/// </summary>
internal static class AutonomousPass
{
    private sealed record Accepted(RawDetection Detection, string DetectorId);

    public static IReadOnlyList<string> Apply(SegmentedText text)
    {
        var warnings = new List<string>();
        var freeSegments = text.FreeSegments();
        var acceptedBySegment = new Dictionary<int, List<Accepted>>();

        IDetector[] detectors =
        [
            new WindowsUserPathDetector(),   // D-01
            new UncHostDetector(),           // D-02
            new EmailDetector(),             // D-03
            new IpV4Detector(),              // D-04
            new FrenchPhoneDetector(),       // D-05
            new IbanDetector(),              // D-06
            new NirDetector(),               // D-07
            new CreditCardDetector(),        // D-08
            new GuidDetector(),              // D-09
            new SecretDetector(),            // D-10
            new InternalUrlDetector(),       // D-11
        ];

        foreach (var detector in detectors)
        {
            try
            {
                foreach ((int index, string segmentText) in freeSegments)
                {
                    foreach (var detection in detector.Detect(segmentText))
                    {
                        TryAccept(acceptedBySegment, index, detection, detector.Id);
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                warnings.Add($"Détecteur {detector.Id} ignoré (analyse trop longue sur ce texte).");
            }
        }

        DetectUserNameEchoes(freeSegments, acceptedBySegment);

        var tokens = AssignTokens(acceptedBySegment);
        var edits = new Dictionary<int, List<SegmentEdit>>();
        foreach ((int index, var acceptedList) in acceptedBySegment)
        {
            edits[index] = acceptedList
                .OrderBy(a => a.Detection.Start)
                .Select(a => new SegmentEdit(
                    a.Detection.Start,
                    a.Detection.Length,
                    a.Detection.Kind == DetectionKind.Replace
                        ? tokens[(a.Detection.TokenType, Normalize(a.Detection.Value))]
                        : null,
                    a.DetectorId))
                .ToList();
        }

        text.ApplyEdits(edits);
        return warnings;
    }

    /// <summary>D-12 (FR-4.3) : chaque nom d'utilisateur extrait d'un chemin par
    /// D-01 est aussi remplacé partout où il apparaît isolé dans le texte.</summary>
    private static void DetectUserNameEchoes(
        IReadOnlyList<(int Index, string Text)> freeSegments,
        Dictionary<int, List<Accepted>> acceptedBySegment)
    {
        var userNames = acceptedBySegment.Values
            .SelectMany(list => list)
            .Where(a => a.DetectorId == "D-01")
            .Select(a => a.Detection.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string name in userNames)
        {
            var pattern = new Regex(
                $@"\b{Regex.Escape(name)}\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
            foreach ((int index, string segmentText) in freeSegments)
            {
                foreach (Match match in pattern.Matches(segmentText))
                {
                    // Valeur canonique = celle de D-01, pour partager le même jeton.
                    TryAccept(
                        acceptedBySegment,
                        index,
                        new RawDetection(match.Index, match.Length, DetectionKind.Replace, TokenTypes.User, name),
                        "D-12");
                }
            }
        }
    }

    private static void TryAccept(
        Dictionary<int, List<Accepted>> acceptedBySegment,
        int segmentIndex,
        RawDetection detection,
        string detectorId)
    {
        if (!acceptedBySegment.TryGetValue(segmentIndex, out var list))
        {
            list = [];
            acceptedBySegment[segmentIndex] = list;
        }

        int end = detection.Start + detection.Length;
        foreach (var other in list)
        {
            int otherEnd = other.Detection.Start + other.Detection.Length;
            if (detection.Start < otherEnd && other.Detection.Start < end)
            {
                return;
            }
        }

        list.Add(new Accepted(detection, detectorId));
    }

    /// <summary>Jetons stables : même valeur ⇒ même jeton ; une seule valeur d'un
    /// type ⇒ XX_TYPE_XX ; plusieurs valeurs distinctes ⇒ XX_TYPE_1_XX, XX_TYPE_2_XX…
    /// numérotées par ordre d'apparition dans le texte.</summary>
    private static Dictionary<(string Type, string Value), string> AssignTokens(
        Dictionary<int, List<Accepted>> acceptedBySegment)
    {
        var valuesByType = new Dictionary<string, List<string>>();
        var seenByType = new Dictionary<string, HashSet<string>>();

        var replacementsInOrder = acceptedBySegment
            .OrderBy(kv => kv.Key)
            .SelectMany(kv => kv.Value.OrderBy(a => a.Detection.Start))
            .Where(a => a.Detection.Kind == DetectionKind.Replace);

        foreach (var accepted in replacementsInOrder)
        {
            string type = accepted.Detection.TokenType;
            if (!seenByType.TryGetValue(type, out var seen))
            {
                seen = new HashSet<string>(StringComparer.Ordinal);
                seenByType[type] = seen;
                valuesByType[type] = [];
            }

            if (seen.Add(Normalize(accepted.Detection.Value)))
            {
                valuesByType[type].Add(accepted.Detection.Value);
            }
        }

        var tokens = new Dictionary<(string, string), string>();
        foreach ((string type, var values) in valuesByType)
        {
            for (int i = 0; i < values.Count; i++)
            {
                string token = values.Count == 1 ? $"XX_{type}_XX" : $"XX_{type}_{i + 1}_XX";
                tokens[(type, Normalize(values[i]))] = token;
            }
        }

        return tokens;
    }

    private static string Normalize(string value) => value.ToUpperInvariant();
}
