# prompt-cleaner

Application Windows fenêtrée **100 % portable** (un seul `.exe`, aucune installation) qui nettoie un texte de ses données personnelles/confidentielles et copie automatiquement le résultat dans le presse-papier.

## Fonctionnement en bref

1. **Charger config…** : sélection via l'explorateur Windows d'un fichier de règles `motclé : remplacement` :
   ```
   google : mon-entreprise
   fb44ja8k : nom-user
   myApp : nom-application
   ```
2. L'utilisateur colle son texte et clique sur **Clean**.
3. **Passe 1** : remplacement de toutes les occurrences des mots-clés du dictionnaire.
4. **Passe 2 (autonome)** : détection résiduelle — chemins `C:\Users\<nom>` (→ `XX_USER_XX`), e-mails, IP, téléphones, IBAN, cartes bancaires… remplacés ; GUID, secrets probables et URLs internes signalés en alerte.
5. Le résultat s'affiche avec un code couleur (légende incluse) et est **copié automatiquement dans le presse-papier** :
   - 🟩 **vert** : mot remplacé,
   - 🟥 **rouge** : donnée suspecte non remplacée, à vérifier manuellement.

## Technologie retenue

**C# / .NET 8 WinForms**, publié en *self-contained single-file* (`win-x64`) : un unique `prompt-cleaner.exe` embarquant le runtime — double-clic sur tout Windows 10/11, y compris depuis une clé USB, sans droits administrateur. Hors-ligne par conception : aucun appel réseau, aucune écriture disque du texte traité. Le choix est argumenté (matrice de décision) dans `docs/architecture.md`.

## Documentation projet (méthode BMAD)

| Phase BMAD | Document |
|---|---|
| 1. Analyste — Project Brief | [`docs/brief.md`](docs/brief.md) |
| 2. PM — PRD (exigences, UX, épics) | [`docs/prd.md`](docs/prd.md) |
| 3. Architecte — Architecture & choix techno | [`docs/architecture.md`](docs/architecture.md) |
| 4. PO/SM — Backlog (stories, critères, itérations) | [`docs/backlog.md`](docs/backlog.md) |

## État d'avancement

| Itération (cf. `docs/backlog.md`) | Contenu | État |
|---|---|---|
| 1 | Solution .NET, fenêtre principale, publication portable, CI | ✅ fait |
| 2 | Fichier de config (parseur, explorateur, auto-chargement), passe dictionnaire | ✅ fait |
| 3–4 | Détecteurs autonomes D-01 à D-12 | ⏳ à venir |
| 5 | Boucle presse-papier + rendu couleur | ✅ anticipé (déjà câblé) |
| 6 | Durcissement, accessibilité, release v1.0.0 | ⏳ à venir |

## Développement

```bash
dotnet build          # compile la solution (Windows, Linux ou macOS)
dotnet test           # exécute les tests unitaires du moteur

# Produit l'exécutable portable unique (publish/prompt-cleaner.exe) :
dotnet publish src/PromptCleaner.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

La CI GitHub Actions (`.github/workflows/ci.yml`) rejoue build + tests + publication à chaque push et attache l'exécutable en artefact ; un tag `v*` crée une Release GitHub avec l'exe en pièce jointe.

Des fichiers d'exemple sont fournis dans `samples/` (`regles-exemple.cfg`, `texte-exemple.txt`).

## Avertissement

prompt-cleaner est une aide à l'anonymisation : les alertes rouges exigent une vérification humaine, et l'absence d'alerte ne garantit pas l'absence de donnée sensible. Le presse-papier est écrasé à chaque nettoyage.
