namespace PromptCleaner.Core.Model;

/// <summary>Compteurs affichés dans la barre d'état après un nettoyage.</summary>
public sealed record CleanStats(int ConfigReplacements, int AutoReplacements, int Alerts)
{
    public int TotalReplacements => ConfigReplacements + AutoReplacements;
}
