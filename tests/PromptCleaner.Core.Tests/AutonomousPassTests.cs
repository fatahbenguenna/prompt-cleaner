using PromptCleaner.Core.Config;
using PromptCleaner.Core.Model;
using PromptCleaner.Core.Pipeline;
using Xunit;

namespace PromptCleaner.Core.Tests;

/// <summary>Tests de la passe autonome (S4.1 à S4.5), via l'API publique du pipeline.</summary>
public class AutonomousPassTests
{
    private static CleanResult Clean(string text, params ReplacementRule[] rules)
        => new CleaningPipeline().Clean(text, rules);

    // ----- D-01 / D-02 / D-12 : chemins Windows, UNC, écho du nom (S4.2) -----

    [Theory]
    [InlineData(@"C:\Users\jdupont\Documents", @"C:\Users\XX_USER_XX\Documents")]
    [InlineData(@"c:\users\jdupont\src", @"c:\users\XX_USER_XX\src")]
    [InlineData(@"D:\Users\jdupont", @"D:\Users\XX_USER_XX")]
    public void D01_NomUtilisateurDansCheminWindows_EstRemplaceSansToucherLeReste(string input, string expected)
    {
        Assert.Equal(expected, Clean(input).CleanText);
    }

    [Fact]
    public void D01_ProfilsWindowsGeneriques_NeSontPasRemplaces()
    {
        Assert.Equal(@"C:\Users\Public\Desktop", Clean(@"C:\Users\Public\Desktop").CleanText);
    }

    [Fact]
    public void D01_NomAvecEspaceSuiviDUnBackslash_EstRemplaceEnEntier()
    {
        Assert.Equal(@"C:\Users\XX_USER_XX\Documents", Clean(@"C:\Users\John Doe\Documents").CleanText);
    }

    [Fact]
    public void D01_CheminUsersSansNom_NAvalePasLaSuiteDeLaPhrase()
    {
        string input = @"le chemin C:\Users\, l'e-mail et le reste de la phrase";
        Assert.Equal(input, Clean(input).CleanText);
    }

    [Fact]
    public void D01_NomEnFinDePhrase_EstRemplaceSansLaPonctuation()
    {
        Assert.Equal(@"voir C:\Users\XX_USER_XX. Ensuite…", Clean(@"voir C:\Users\jdupont. Ensuite…").CleanText);
    }

    [Fact]
    public void D02_ServeurUnc_EstRemplaceSansToucherLePartage()
    {
        Assert.Equal(@"\\XX_HOST_XX\commun\doc.txt", Clean(@"\\SRV-PARIS\commun\doc.txt").CleanText);
    }

    [Fact]
    public void D12_LeNomReleveDansUnChemin_EstAussiRemplaceAilleursDansLeTexte()
    {
        var result = Clean("le poste de jdupont : C:\\Users\\jdupont\\AppData. Contacter jdupont.");

        Assert.Equal("le poste de XX_USER_XX : C:\\Users\\XX_USER_XX\\AppData. Contacter XX_USER_XX.", result.CleanText);
    }

    [Fact]
    public void D12_UnNomProcheMaisDifferent_NestPasTouche()
    {
        var result = Clean("C:\\Users\\jdupont\\x et l'utilisateur jdupont2");

        Assert.Contains("jdupont2", result.CleanText);
        Assert.DoesNotContain("\\jdupont\\", result.CleanText);
    }

    // ----- D-03 / D-04 / D-05 : e-mails, IPv4, téléphones (S4.3) -----

    [Fact]
    public void D03_EmailUnique_DevientXxEmailXx()
    {
        Assert.Equal("Contact : XX_EMAIL_XX", Clean("Contact : paul.dupont@societe.fr").CleanText);
    }

    [Fact]
    public void D03_DeuxEmailsDistincts_RecoiventDesJetonsNumerotes_EtLeMemeEmailLeMemeJeton()
    {
        var result = Clean("a@x.fr puis b@y.fr puis encore a@x.fr");

        Assert.Equal("XX_EMAIL_1_XX puis XX_EMAIL_2_XX puis encore XX_EMAIL_1_XX", result.CleanText);
    }

    [Theory]
    [InlineData("serveur 192.168.1.42 injoignable", "serveur XX_IP_XX injoignable")]
    [InlineData("ping vers 10.0.0.7.", "ping vers XX_IP_XX.")]
    public void D04_AdresseIpV4_EstRemplacee(string input, string expected)
    {
        Assert.Equal(expected, Clean(input).CleanText);
    }

    [Theory]
    [InlineData("localhost 127.0.0.1 ok")]
    [InlineData("plage de doc 192.0.2.1 ok")]
    [InlineData("pas une ip 999.1.1.1 ok")]
    [InlineData("version 1.2.3.4.5 ok")]
    public void D04_LocalhostPlagesDeDocEtFauxPositifs_SontLaissesIntacts(string input)
    {
        Assert.Equal(input, Clean(input).CleanText);
    }

    [Theory]
    [InlineData("appelez le 06 12 34 56 78 svp", "appelez le XX_TEL_XX svp")]
    [InlineData("appelez le 06.12.34.56.78 svp", "appelez le XX_TEL_XX svp")]
    [InlineData("appelez le +33 6 12 34 56 78 svp", "appelez le XX_TEL_XX svp")]
    public void D05_TelephoneFrancais_EstRemplace(string input, string expected)
    {
        Assert.Equal(expected, Clean(input).CleanText);
    }

    // ----- D-06 / D-07 / D-08 : IBAN, NIR, carte bancaire (S4.4) -----

    [Theory]
    [InlineData("IBAN : FR14 2004 1010 0505 0001 3M02 606", "IBAN : XX_IBAN_XX")]
    [InlineData("IBAN : FR1420041010050500013M02606", "IBAN : XX_IBAN_XX")]
    public void D06_IbanValide_EstRemplace(string input, string expected)
    {
        Assert.Equal(expected, Clean(input).CleanText);
    }

    [Fact]
    public void D06_ChaineRessemblantAUnIbanMaisChecksumInvalide_NestPasTouchee()
    {
        string input = "IBAN : FR15 2004 1010 0505 0001 3M02 606";
        Assert.Equal(input, Clean(input).CleanText);
    }

    [Theory]
    [InlineData("NIR 1 85 05 78 006 084 91 fin", "NIR XX_NIR_XX fin")]
    [InlineData("NIR 185057800608491 fin", "NIR XX_NIR_XX fin")]
    public void D07_NirAvecCleValide_EstRemplace(string input, string expected)
    {
        Assert.Equal(expected, Clean(input).CleanText);
    }

    [Fact]
    public void D07_NirAvecCleInvalide_NestPasTouche()
    {
        string input = "NIR 1 85 05 78 006 084 92 fin";
        Assert.Equal(input, Clean(input).CleanText);
    }

    [Theory]
    [InlineData("CB 4539 1488 0343 6467 ok", "CB XX_CB_XX ok")]
    [InlineData("CB 4539148803436467 ok", "CB XX_CB_XX ok")]
    public void D08_CarteBancaireValideeLuhn_EstRemplacee(string input, string expected)
    {
        Assert.Equal(expected, Clean(input).CleanText);
    }

    [Fact]
    public void D08_SuiteDeChiffresEchouantLuhn_NestPasTouchee()
    {
        string input = "CB 4539 1488 0343 6468 ok";
        Assert.Equal(input, Clean(input).CleanText);
    }

    // ----- D-09 / D-10 / D-11 : alertes (S4.5) -----

    [Fact]
    public void D09_Guid_EstAlerteSansEtreModifie()
    {
        string input = "id 550e8400-e29b-41d4-a716-446655440000 fin";
        var result = Clean(input);

        Assert.Equal(input, result.CleanText);
        var span = Assert.Single(result.Spans);
        Assert.Equal(SpanKind.Alert, span.Kind);
        Assert.Equal("D-09", span.DetectorId);
        Assert.Equal(1, result.Stats.Alerts);
    }

    [Theory]
    [InlineData("cle sk-abc123def456ghi789jkl012 fin")]
    [InlineData("token ghp_abcdefghij1234567890KLMNOP fin")]
    [InlineData("aws AKIAIOSFODNN7EXAMPLE fin")]
    [InlineData("jeton aB3dE5gH7jK9mN1pQ4sT fin")]
    public void D10_SecretProbable_EstAlerteSansEtreModifie(string input)
    {
        var result = Clean(input);

        Assert.Equal(input, result.CleanText);
        var span = Assert.Single(result.Spans);
        Assert.Equal(SpanKind.Alert, span.Kind);
        Assert.Equal("D-10", span.DetectorId);
    }

    [Fact]
    public void D10_HashHexadecimalPur_NestPasAlerte()
    {
        var result = Clean("commit 3f786850e387550fdab836ed7e6dc881de23001b fin");

        Assert.Empty(result.Spans);
    }

    [Fact]
    public void D11_UrlInterne_EstAlertee_UrlPubliqueConnueIgnoree()
    {
        var result = Clean("voir https://intranet.societe.fr/page et https://github.com/org/repo");

        Assert.Equal("voir https://intranet.societe.fr/page et https://github.com/org/repo", result.CleanText);
        var span = Assert.Single(result.Spans);
        Assert.Equal("D-11", span.DetectorId);
        Assert.Equal("https://intranet.societe.fr/page", span.Original);
    }

    // ----- S4.1 : cadre commun -----

    [Fact]
    public void PasseAutonome_NAnalysePas_LesZonesDejaRemplaceesParLaConfig()
    {
        // "paul.dupont@societe.fr" est couvert par la config : l'e-mail ne doit
        // pas être re-détecté, et le jeton config est conservé tel quel.
        var result = Clean("mail : paul.dupont@societe.fr", new ReplacementRule("paul.dupont@societe.fr", "contact-anonyme"));

        Assert.Equal("mail : contact-anonyme", result.CleanText);
        Assert.Equal(1, result.Stats.ConfigReplacements);
        Assert.Equal(0, result.Stats.AutoReplacements);
    }

    [Fact]
    public void PasseAutonome_LesStatistiques_DistinguentConfigAutoEtAlertes()
    {
        var result = Clean(
            "user google : C:\\Users\\jdupont\\x, mail a@x.fr, id 550e8400-e29b-41d4-a716-446655440000",
            new ReplacementRule("google", "mon-entreprise"));

        Assert.Equal(1, result.Stats.ConfigReplacements);
        Assert.Equal(2, result.Stats.AutoReplacements); // jdupont + a@x.fr
        Assert.Equal(1, result.Stats.Alerts);           // le GUID
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void PasseAutonome_TousLesSpansRemplaces_CorrespondentAuTexteFinal()
    {
        var result = Clean("C:\\Users\\jdupont\\x, a@x.fr, b@y.fr, 192.168.1.42, 06 12 34 56 78");

        foreach (var span in result.Spans)
        {
            string extract = result.CleanText.Substring(span.Start, span.Length);
            if (span.Kind == SpanKind.Replaced)
            {
                Assert.StartsWith("XX_", extract);
                Assert.EndsWith("_XX", extract);
            }
            else
            {
                Assert.Equal(span.Original, extract);
            }
        }
    }
}
