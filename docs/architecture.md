# Architecture — prompt-cleaner

> **Méthode BMAD — Phase 3 : Architecte**
> Document produit par le rôle *Architect* à partir du PRD (`docs/prd.md`).
> Il fixe le choix technologique, la structure du code et les décisions techniques structurantes.

## 1. Choix de la technologie

### 1.1 Exigences qui pilotent le choix

1. Un **seul `.exe` portable**, sans installation ni runtime préexistant (NFR-1).
2. Cible **Windows uniquement** (NFR-2) : dialogue de fichiers de l'explorateur, presse-papier Windows, chemins `C:\Users\…`.
3. Affichage de **texte riche coloré** (vert/rouge) performant (FR-5).
4. Développement et maintenance simples pour une application mono-fenêtre.

### 1.2 Matrice de décision

| Option | Portable 1 fichier sans prérequis | Texte coloré natif | Taille exe | Effort de dev | Risques | Verdict |
|---|---|---|---|---|---|---|
| **C# .NET 8 WinForms (publish self-contained single-file)** | ✅ oui (runtime embarqué) | ✅ `RichTextBox` natif | ~65–80 Mo (compressé) | ✅ très faible | Taille de l'exe (acceptable) | ✅ **RETENU** |
| Tauri v2 (Rust + WebView2) | ⚠️ dépend de WebView2 (préinstallé Win11, pas garanti Win10/postes verrouillés) | ✅ HTML/CSS | ~8 Mo | Moyen (2 langages) | Dépendance WebView2 = contraire à « très portable » | ❌ |
| Python + Tkinter + PyInstaller onefile | ✅ oui | ⚠️ tags Tk corrects mais lents sur gros textes | ~15–25 Mo | Faible | Faux positifs antivirus fréquents, démarrage lent (extraction temp) | ❌ |
| Go + Walk (Win32 natif) | ✅ oui | ⚠️ RichEdit via bindings, peu documenté | ~10 Mo | Élevé (écosystème GUI Go limité) | Lib GUI peu maintenue | ❌ |
| Electron | ❌ dossier ou exe > 150 Mo | ✅ | énorme | Faible | Contraire à l'esprit « portable léger » | ❌ |
| C++ Win32/MFC | ✅ oui, ~1 Mo | ✅ RichEdit | minime | Très élevé | Coût de dev/maintenance disproportionné | ❌ |

### 1.3 Décision

> **C# / .NET 8 (LTS) + WinForms, publié en *self-contained, single-file, win-x64*.**

Justification :
- `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true` produit **un unique `.exe`** qui embarque le runtime : double-clic sur n'importe quel Windows 10/11, clé USB comprise, **aucune installation** — exactement le besoin.
- WinForms fournit **tout le nécessaire en natif** : `OpenFileDialog` (explorateur Windows demandé au point 3 du besoin), `Clipboard.SetText/GetText`, `RichTextBox` avec coloration par segments (`SelectionBackColor`) idéale pour le rendu vert/rouge.
- Regex .NET (`System.Text.RegularExpressions`, avec timeout) pour les détecteurs de la passe autonome.
- Un seul langage, testable avec xUnit, CI GitHub Actions triviale (`windows-latest`).
- La taille (~70 Mo) est le seul compromis, jugé acceptable (brief §8) ; une publication *framework-dependent* (~200 Ko, nécessite .NET installé) reste possible en second artefact pour les postes équipés.

## 2. Vue d'ensemble

Application **mono-processus, mono-fenêtre**, sans persistance et sans réseau. Architecture en deux couches pour isoler la logique testable de l'UI :

```
┌────────────────────────────────────────────────────────────┐
│  PromptCleaner.App  (WinForms, non testé unitairement)     │
│  MainForm : zones de texte, boutons, légende, statut       │
│  ClipboardService / FileDialogService (wrappers UI)        │
└──────────────────────────┬─────────────────────────────────┘
                           │ appelle (aucune dépendance inverse)
┌──────────────────────────▼─────────────────────────────────┐
│  PromptCleaner.Core  (bibliothèque pure, 100 % testable)   │
│                                                            │
│  ConfigParser        fichier .cfg → List<ReplacementRule>  │
│  CleaningPipeline    orchestre Passe 1 puis Passe 2        │
│   ├─ DictionaryPass  remplacements du dictionnaire         │
│   └─ AutonomousPass  IDetector[] (D-01 … D-12)             │
│  CleanResult         texte final + List<Span>              │
└────────────────────────────────────────────────────────────┘
```

## 3. Modèle de données central : les *spans*

Le moteur ne renvoie pas seulement du texte : il renvoie le texte final **plus la liste des segments annotés**, ce qui découple totalement le nettoyage du rendu couleur.

```csharp
enum SpanKind { Replaced, Alert }          // vert / rouge
record Span(int Start, int Length, SpanKind Kind, string DetectorId, string Original);

record CleanResult(
    string CleanText,                      // copié tel quel dans le presse-papier
    IReadOnlyList<Span> Spans,             // positions DANS CleanText
    CleanStats Stats);                     // compteurs pour la barre d'état
```

Règles d'implémentation :
- Les remplacements sont appliqués **de la fin vers le début** du texte (ou via reconstruction par segments) pour garder des offsets valides.
- Un caractère déjà couvert par un span `Replaced` est **exclu** des détections suivantes (FR-3.3) — évite les remplacements en cascade.
- Le mapping `valeur originale → jeton` est un `Dictionary<string,string>` par exécution : même valeur ⇒ même jeton, valeurs distinctes ⇒ jetons numérotés (FR-4.1/4.2).

## 4. Passe 1 — Dictionnaire

- Tri des règles par **longueur de mot-clé décroissante** (FR-3.2).
- Recherche insensible à la casse via `Regex.Escape(motclé)` + `RegexOptions.IgnoreCase` (pas de `\b` imposé : `myApp` doit matcher `myApp2024` ? → non ; par défaut correspondance **littérale sans frontière de mot**, fidèle au besoin « toutes les occurrences »).
- Chaque match produit un span `Replaced` avec `DetectorId = "config"`.

## 5. Passe 2 — Détecteurs autonomes

Chaque détecteur implémente :

```csharp
interface IDetector {
    string Id { get; }                     // "D-01"
    DetectorAction Action { get; }         // Replace | Alert
    IEnumerable<Detection> Detect(string text, IReadOnlySet<Range> excluded);
}
```

Points de conception :
- **D-01 (chemins Windows)** : regex `(?i)(?<root>[a-z]:\\Users\\)(?<user>[^\\/:*?"<>|\r\n]+)` → remplace uniquement le groupe `user` par `XX_USER_XX`, préservant le reste du chemin. Le nom extrait alimente **D-12** qui remplace ses autres occurrences isolées (`\b<nom>\b`).
- **D-04 (IPv4)** : exclusions `127.0.0.1`, `0.0.0.0`, plages de documentation (`192.0.2.*`, `198.51.100.*`, `203.0.113.*`) pour limiter les faux positifs.
- **D-08 (CB)** : candidat regex 13–19 chiffres (espaces/tirets tolérés) validé par **algorithme de Luhn** avant remplacement.
- **D-10 (secrets)** : deux voies — préfixes connus (`sk-`, `ghp_`, `gho_`, `AKIA`, `xox[bp]-`, `Bearer `) et heuristique d'entropie de Shannon (> 4,0 bits/char sur ≥ 20 caractères alphanumériques mixtes) ⇒ **Alert** seulement (jamais de remplacement automatique, trop de faux positifs possibles : hash git, base64 légitime…).
- Toutes les regex sont compilées (`RegexOptions.Compiled`) avec `matchTimeout` (100 ms) — un texte pathologique ne gèle pas l'application (NFR-5).
- L'ordre d'exécution des détecteurs est fixe (D-01 → D-12) et chaque détecteur reçoit les zones exclues accumulées.

## 6. UI (PromptCleaner.App)

- **`MainForm`** unique (pas de MVVM lourd pour une fenêtre) ; la logique reste dans `Core`, le form ne fait que brancher événements ↔ pipeline.
- Rendu couleur : itération sur `CleanResult.Spans` puis `RichTextBox.Select(start, length)` + `SelectionBackColor` (vert pâle `#C8F7C5` / rouge pâle `#F7C5C5`) et `SelectionColor` foncés — fond **et** teinte pour l'accessibilité (NFR-6). `rtb.SuspendLayout()`/`WM_SETREDRAW` pendant l'application des styles pour la performance.
- Presse-papier : `Clipboard.SetText(result.CleanText)` immédiatement après le nettoyage (FR-6.1) ; lecture via `Clipboard.ContainsText()`/`GetText()` (FR-2.2).
- Textes > 100 Ko : pipeline exécuté dans `Task.Run`, bouton **Clean** désactivé pendant le calcul, UI réactive (NFR-4).
- Auto-chargement au démarrage : recherche de `prompt-cleaner.cfg` dans `AppContext.BaseDirectory` (FR-1.6). **Attention single-file** : utiliser `AppContext.BaseDirectory` (répertoire réel de l'exe), pas `Assembly.Location` (vide en single-file).

## 7. Structure du dépôt

```
prompt-cleaner/
├── docs/                      # artefacts BMAD (brief, prd, architecture, backlog)
├── src/
│   ├── PromptCleaner.Core/    # moteur pur (netstandard-friendly, aucun WinForms)
│   │   ├── Config/            # ConfigParser, ReplacementRule
│   │   ├── Pipeline/          # CleaningPipeline, DictionaryPass, AutonomousPass
│   │   ├── Detectors/         # D01WindowsUserPath.cs … D12KnownUserEcho.cs
│   │   └── Model/             # Span, CleanResult, CleanStats
│   └── PromptCleaner.App/     # WinForms (net8.0-windows)
│       ├── MainForm.cs
│       └── Services/          # ClipboardService, FileDialogService
├── tests/
│   └── PromptCleaner.Core.Tests/   # xUnit : parseur, passes, chaque détecteur
├── samples/
│   ├── regles-exemple.cfg
│   └── texte-exemple.txt
├── .github/workflows/ci.yml   # build + tests + publish artefact exe
└── PromptCleaner.sln
```

## 8. Build & livraison

- **Dev** : `dotnet build` / `dotnet test` (les tests `Core` tournent aussi sous Linux en CI ; seul le projet App exige Windows).
- **Release portable** :
  ```
  dotnet publish src/PromptCleaner.App -c Release -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true
  ```
  Produit `prompt-cleaner.exe` unique (~70 Mo). *Note : le trimming (`PublishTrimmed`) n'est pas supporté par WinForms — ne pas l'activer.*
- **CI (GitHub Actions, `windows-latest`)** : restore → build → test → publish → upload de l'exe en artefact ; création d'une Release GitHub sur tag `v*`.

## 9. Sécurité & confidentialité

- Aucun appel réseau ; aucune dépendance NuGet externe dans `Core` (surface d'attaque et taille minimales).
- Aucune écriture disque du texte traité ; pas de logs contenant des données utilisateur.
- Le presse-papier est le seul canal de sortie — comportement documenté dans le README (l'utilisateur doit savoir que le presse-papier est écrasé).
- Regex avec timeout (anti-ReDoS).

## 10. Décisions actées (ADR courts)

| # | Décision | Alternative rejetée | Raison |
|---|---|---|---|
| ADR-1 | .NET 8 WinForms self-contained single-file | Tauri/WebView2 | WebView2 non garanti = pas « très portable » |
| ADR-2 | Deux projets `Core`/`App` | Tout dans le form | Testabilité du moteur (NFR-8) |
| ADR-3 | Spans retournés par le moteur, couleur appliquée par l'UI | HTML/RTF généré par le moteur | Découplage, tests simples |
| ADR-4 | Alertes (rouge) jamais auto-remplacées pour GUID/secrets/URL | Tout remplacer | Faux positifs destructeurs (hash git, base64…) |
| ADR-5 | Format config texte `clé : valeur` | JSON/YAML | Fidèle à l'exemple du besoin, éditable par tous |
