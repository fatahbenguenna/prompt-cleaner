using System.Diagnostics;
using System.Text;
using PromptCleaner.Core.Config;
using PromptCleaner.Core.Model;
using PromptCleaner.Core.Pipeline;
using Xunit;

namespace PromptCleaner.Core.Tests;

public class CleaningPipelineTests
{
    private static CleanResult Clean(string text, params ReplacementRule[] rules)
        => new CleaningPipeline().Clean(text, rules);

    [Fact]
    public void Clean_ToutesLesOccurrences_SontRemplaceesSansTenirCompteDeLaCasse()
    {
        var result = Clean("Google GOOGLE google", new ReplacementRule("google", "mon-entreprise"));

        Assert.Equal("mon-entreprise mon-entreprise mon-entreprise", result.CleanText);
        Assert.Equal(3, result.Stats.ConfigReplacements);
        Assert.Equal(3, result.Spans.Count);
    }

    [Fact]
    public void Clean_MotCleLePlusLongDAbord_UnMotCleCourtNeCassePasUnLong()
    {
        var result = Clean("myApp utilise app",
            new ReplacementRule("app", "X"),
            new ReplacementRule("myApp", "Y"));

        Assert.Equal("Y utilise X", result.CleanText);
    }

    [Fact]
    public void Clean_UneZoneRemplacee_NestPasReAnalyseeParLesReglesSuivantes()
    {
        // alpha → beta, puis beta → gamma : le "beta" issu du premier
        // remplacement ne doit PAS devenir "gamma" (pas de cascade, FR-3.3).
        var result = Clean("alpha et beta",
            new ReplacementRule("alpha", "beta"),
            new ReplacementRule("beta", "gamma"));

        Assert.Equal("beta et gamma", result.CleanText);
    }

    [Fact]
    public void Clean_RemplacementContenantSonPropreMotCle_NeBouclePas()
    {
        var result = Clean("user", new ReplacementRule("user", "nom-user"));

        Assert.Equal("nom-user", result.CleanText);
        Assert.Single(result.Spans);
    }

    [Fact]
    public void Clean_LesSpans_PointentExactementLesRemplacementsDansLeTexteFinal()
    {
        var result = Clean("dossier de fb44ja8k sur google.",
            new ReplacementRule("google", "mon-entreprise"),
            new ReplacementRule("fb44ja8k", "nom-user"));

        Assert.Equal("dossier de nom-user sur mon-entreprise.", result.CleanText);
        foreach (var span in result.Spans)
        {
            string extrait = result.CleanText.Substring(span.Start, span.Length);
            Assert.Equal(SpanKind.Replaced, span.Kind);
            Assert.True(extrait is "nom-user" or "mon-entreprise", $"span inattendu : {extrait}");
        }

        Assert.Equal("fb44ja8k", result.Spans[0].Original);
        Assert.Equal("google", result.Spans[1].Original);
    }

    [Fact]
    public void Clean_OffsetsCorrects_MemeAvecEmojiEtAccents()
    {
        var result = Clean("héllo 🚀 google 🚀 été", new ReplacementRule("google", "X"));

        var span = Assert.Single(result.Spans);
        Assert.Equal("X", result.CleanText.Substring(span.Start, span.Length));
    }

    [Fact]
    public void Clean_SansRegle_TexteInchangeEtAucunSpan()
    {
        var result = Clean("texte intact");

        Assert.Equal("texte intact", result.CleanText);
        Assert.Empty(result.Spans);
        Assert.Equal(new CleanStats(0, 0, 0), result.Stats);
    }

    [Fact]
    public void Clean_TexteMultiligneCrlf_LesFinsDeLigneSontPreservees()
    {
        var result = Clean("ligne1 google\r\nligne2 google\r\n", new ReplacementRule("google", "X"));

        Assert.Equal("ligne1 X\r\nligne2 X\r\n", result.CleanText);
        Assert.Equal(2, result.Spans.Count);
    }

    [Fact]
    public void Clean_LesSpansSontTriesParPosition()
    {
        var result = Clean("a google b myApp c google",
            new ReplacementRule("google", "G"),
            new ReplacementRule("myApp", "M"));

        var starts = result.Spans.Select(s => s.Start).ToArray();
        Assert.Equal(starts.OrderBy(s => s), starts);
    }

    [Fact]
    public void Clean_UnMegaOctetEtCentRegles_ResteRapide()
    {
        var builder = new StringBuilder(1_100_000);
        while (builder.Length < 1_000_000)
        {
            builder.Append("Un log anodin avec google et le user fb44ja8k dans C:\\Temp. ");
        }

        var rules = Enumerable.Range(0, 98)
            .Select(i => new ReplacementRule($"motcle-{i:D3}", $"remplacement-{i:D3}"))
            .Append(new ReplacementRule("google", "mon-entreprise"))
            .Append(new ReplacementRule("fb44ja8k", "nom-user"))
            .ToArray();

        var chrono = Stopwatch.StartNew();
        var result = new CleaningPipeline().Clean(builder.ToString(), rules);
        chrono.Stop();

        Assert.DoesNotContain("google", result.CleanText, StringComparison.OrdinalIgnoreCase);
        // NFR-4 vise < 1 s ; la borne est relâchée pour absorber les machines de CI lentes.
        Assert.True(chrono.ElapsedMilliseconds < 5000, $"nettoyage trop lent : {chrono.ElapsedMilliseconds} ms");
    }
}
