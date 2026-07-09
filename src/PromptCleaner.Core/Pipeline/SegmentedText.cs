using System.Text;
using PromptCleaner.Core.Model;

namespace PromptCleaner.Core.Pipeline;

/// <summary>
/// Texte découpé en segments libres ou verrouillés. Un segment verrouillé
/// (remplacement effectué ou alerte posée) n'est plus jamais re-analysé par les
/// règles suivantes (FR-3.3 : pas de remplacement en cascade), et les offsets
/// finaux sont recalculés à la composition — jamais maintenus à la main.
/// </summary>
internal sealed class SegmentedText
{
    private sealed record Segment(string Text, bool Locked, SpanKind Kind, string DetectorId, string Original)
    {
        public static Segment Free(string text) => new(text, false, SpanKind.Replaced, "", "");
    }

    private List<Segment> _segments;

    public SegmentedText(string text)
    {
        _segments = [Segment.Free(text)];
    }

    /// <summary>Remplace toutes les occurrences littérales de <paramref name="keyword"/>
    /// (casse ignorée) dans les segments encore libres.</summary>
    public void ReplaceLiteral(string keyword, string replacement, string detectorId)
    {
        if (keyword.Length == 0)
        {
            return;
        }

        var next = new List<Segment>(_segments.Count);
        foreach (var segment in _segments)
        {
            if (segment.Locked)
            {
                next.Add(segment);
                continue;
            }

            string text = segment.Text;
            int position = 0;
            int match;
            while ((match = text.IndexOf(keyword, position, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                if (match > position)
                {
                    next.Add(Segment.Free(text[position..match]));
                }

                next.Add(new Segment(
                    replacement,
                    Locked: true,
                    SpanKind.Replaced,
                    detectorId,
                    Original: text.Substring(match, keyword.Length)));
                position = match + keyword.Length;
            }

            if (position == 0)
            {
                next.Add(segment);
            }
            else if (position < text.Length)
            {
                next.Add(Segment.Free(text[position..]));
            }
        }

        _segments = next;
    }

    /// <summary>Concatène les segments et produit les spans annotés,
    /// positionnés dans le texte final.</summary>
    public (string Text, IReadOnlyList<TextSpan> Spans) Compose()
    {
        var builder = new StringBuilder();
        var spans = new List<TextSpan>();
        foreach (var segment in _segments)
        {
            if (segment.Locked)
            {
                spans.Add(new TextSpan(builder.Length, segment.Text.Length, segment.Kind, segment.DetectorId, segment.Original));
            }

            builder.Append(segment.Text);
        }

        return (builder.ToString(), spans);
    }
}
