using PromptCleaner.Core.Config;
using Xunit;

namespace PromptCleaner.Core.Tests;

public class ConfigParserTests
{
    [Fact]
    public void Parse_ExempleDuBesoin_ProduitTroisRegles()
    {
        var report = ConfigParser.Parse("""
            google : mon-entreprise
            fb44ja8k:nom-user
            myApp : nom-application
            """);

        Assert.Equal(0, report.IgnoredLineCount);
        Assert.Collection(report.Rules,
            r => Assert.Equal(new ReplacementRule("google", "mon-entreprise"), r),
            r => Assert.Equal(new ReplacementRule("fb44ja8k", "nom-user"), r),
            r => Assert.Equal(new ReplacementRule("myApp", "nom-application"), r));
    }

    [Fact]
    public void Parse_LignesVidesEtCommentaires_SontIgnoreesSansEtreComptees()
    {
        var report = ConfigParser.Parse("\n# commentaire\n\n  # autre commentaire\ngoogle : x\n\n");

        Assert.Single(report.Rules);
        Assert.Equal(0, report.IgnoredLineCount);
    }

    [Theory]
    [InlineData("pas-de-separateur")]
    [InlineData(": valeur-sans-cle")]
    [InlineData("cle-sans-valeur :")]
    [InlineData(":")]
    public void Parse_LigneInvalide_EstCompteeSansInterrompreLeChargement(string invalidLine)
    {
        var report = ConfigParser.Parse($"google : x\n{invalidLine}\nmyApp : y");

        Assert.Equal(2, report.Rules.Count);
        Assert.Equal(1, report.IgnoredLineCount);
    }

    [Fact]
    public void Parse_LaPremiereOccurrenceDeDeuxPointsSepare_LaValeurPeutContenirDesDeuxPoints()
    {
        var report = ConfigParser.Parse("intranet : https://exemple.fr:8080/portail");

        var rule = Assert.Single(report.Rules);
        Assert.Equal("intranet", rule.Keyword);
        Assert.Equal("https://exemple.fr:8080/portail", rule.Replacement);
    }

    [Fact]
    public void Parse_DoublonInsensibleALaCasse_LaDerniereLigneGagne()
    {
        var report = ConfigParser.Parse("Google : premier\ngoogle : second");

        var rule = Assert.Single(report.Rules);
        Assert.Equal("second", rule.Replacement);
        Assert.Equal(1, report.DuplicateKeywordCount);
    }

    [Fact]
    public void Parse_BomResiduel_NEmpechePasLaPremiereRegle()
    {
        var report = ConfigParser.Parse("\uFEFF" + "google : x");

        var rule = Assert.Single(report.Rules);
        Assert.Equal("google", rule.Keyword);
    }

    [Fact]
    public void Parse_FinsDeLigneWindows_SontGerees()
    {
        var report = ConfigParser.Parse("google : x\r\nmyApp : y\r\n");

        Assert.Equal(2, report.Rules.Count);
        Assert.Equal("y", report.Rules[1].Replacement);
    }

    [Fact]
    public void Parse_ContenuVide_DonneZeroRegleSansErreur()
    {
        var report = ConfigParser.Parse("");

        Assert.Empty(report.Rules);
        Assert.Equal(0, report.IgnoredLineCount);
    }

    [Fact]
    public void ParseFile_FichierUtf8AvecBom_EstLuCorrectement()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllText(path, "google : mon-entreprise\n", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var report = ConfigParser.ParseFile(path);

            var rule = Assert.Single(report.Rules);
            Assert.Equal("google", rule.Keyword);
            Assert.Equal(path, report.FilePath);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
