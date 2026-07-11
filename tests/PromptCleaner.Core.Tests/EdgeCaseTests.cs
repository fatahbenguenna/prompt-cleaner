using System.Diagnostics;
using System.Text;
using PromptCleaner.Core.Config;
using PromptCleaner.Core.Model;
using PromptCleaner.Core.Pipeline;
using Xunit;

namespace PromptCleaner.Core.Tests;

/// <summary>Campagne de cas limites (S6.1) : le moteur ne doit jamais lever
/// d'exception, quelles que soient l'entrée et la configuration.</summary>
public class EdgeCaseTests
{
    [Fact]
    public void Clean_TexteVide_RendUnResultatVideSansErreur()
    {
        var result = new CleaningPipeline().Clean("", [new ReplacementRule("google", "x")]);

        Assert.Equal("", result.CleanText);
        Assert.Empty(result.Spans);
        Assert.Equal(new CleanStats(0, 0, 0), result.Stats);
    }

    [Fact]
    public void Clean_TexteUniquementEmoji_ResteIntact()
    {
        string input = string.Concat(Enumerable.Repeat("🚀🎉🧹✨🔥", 200));

        var result = new CleaningPipeline().Clean(input, [new ReplacementRule("google", "x")]);

        Assert.Equal(input, result.CleanText);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Clean_FinsDeLigneMixtesCrlfEtLf_SontPreservees()
    {
        string input = "l1 google\r\nl2 google\nl3 google\rl4 google";

        var result = new CleaningPipeline().Clean(input, [new ReplacementRule("google", "X")]);

        Assert.Equal("l1 X\r\nl2 X\nl3 X\rl4 X", result.CleanText);
        Assert.Equal(4, result.Spans.Count);
    }

    [Fact]
    public void Parse_DixMilleRegles_ChargeEtNettoieSansProbleme()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < 10_000; i++)
        {
            builder.Append("motcle-").Append(i).Append(" : remplacement-").Append(i).Append('\n');
        }

        var report = ConfigParser.Parse(builder.ToString());
        Assert.Equal(10_000, report.Rules.Count);

        var result = new CleaningPipeline().Clean("début motcle-9999 fin", report.Rules.ToArray());
        Assert.Equal("début remplacement-9999 fin", result.CleanText);
    }

    [Fact]
    public void Clean_CinqMegaOctets_AboutitSansExceptionNiDetecteurIgnore()
    {
        var builder = new StringBuilder(5_300_000);
        while (builder.Length < 5_000_000)
        {
            builder.Append("Log du poste de fb44ja8k : chemin C:\\Users\\jdupont\\logs, ")
                   .Append("mail admin@societe.fr, ip 10.1.2.3, tout va bien aujourd'hui. ");
        }

        var chrono = Stopwatch.StartNew();
        var result = new CleaningPipeline().Clean(builder.ToString(), [new ReplacementRule("fb44ja8k", "nom-user")]);
        chrono.Stop();

        Assert.Empty(result.Warnings);
        Assert.DoesNotContain("jdupont", result.CleanText);
        Assert.DoesNotContain("admin@societe.fr", result.CleanText);
        // NFR-4 : < 5 s pour 1 Mo ; on reste large pour les machines de CI.
        Assert.True(chrono.ElapsedMilliseconds < 60_000, $"nettoyage trop lent : {chrono.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Clean_ReglesDegenerees_SontToleres()
    {
        // Mot-clé vide (ne devrait pas sortir du parseur, mais le moteur doit
        // rester défensif) et remplacement identique au mot-clé.
        var result = new CleaningPipeline().Clean("un texte stable", [
            new ReplacementRule("", "jamais"),
            new ReplacementRule("stable", "stable"),
        ]);

        Assert.Equal("un texte stable", result.CleanText);
        Assert.Single(result.Spans);
    }

    [Fact]
    public void Clean_MotCleUniquementEspaces_NeProduitPasDeBoucleInfinie()
    {
        var result = new CleaningPipeline().Clean("a b", [new ReplacementRule(" ", "_")]);

        Assert.Equal("a_b", result.CleanText);
    }
}
