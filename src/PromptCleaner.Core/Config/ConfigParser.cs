namespace PromptCleaner.Core.Config;

/// <summary>
/// Parseur du fichier de règles : une règle par ligne au format
/// <c>motclé : remplacement</c>, lignes vides et commentaires <c>#</c> ignorés.
/// </summary>
public static class ConfigParser
{
    public static ConfigLoadReport ParseFile(string path)
    {
        // File.ReadAllText détecte le BOM UTF-8/UTF-16 et lit en UTF-8 par défaut.
        string content = File.ReadAllText(path);
        return Parse(content, path);
    }

    public static ConfigLoadReport Parse(string content, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var rules = new List<ReplacementRule>();
        var indexByKeyword = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int ignored = 0;
        int duplicates = 0;

        // Un BOM résiduel (contenu lu sans détection d'encodage) n'est pas un
        // caractère d'espacement : il ferait échouer la première règle.
        content = content.TrimStart('\uFEFF');

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            // La première occurrence de ':' sépare clé et valeur : la valeur
            // peut donc elle-même contenir des ':' (URL, chemin…).
            int separator = line.IndexOf(':');
            if (separator < 0)
            {
                ignored++;
                continue;
            }

            string keyword = line[..separator].Trim();
            string replacement = line[(separator + 1)..].Trim();
            if (keyword.Length == 0 || replacement.Length == 0)
            {
                ignored++;
                continue;
            }

            var rule = new ReplacementRule(keyword, replacement);
            if (indexByKeyword.TryGetValue(keyword, out int existing))
            {
                // Doublon (casse ignorée) : la dernière ligne gagne (FR / S2.1).
                duplicates++;
                rules[existing] = rule;
            }
            else
            {
                indexByKeyword[keyword] = rules.Count;
                rules.Add(rule);
            }
        }

        return new ConfigLoadReport(rules, ignored, duplicates, filePath);
    }
}
