# prompt-cleaner

Application Windows fenêtrée **100 % portable** (un seul `.exe`, aucune installation) qui nettoie un texte de ses données personnelles/confidentielles et copie automatiquement le résultat dans le presse-papier.

## Téléchargement

Récupérez `prompt-cleaner.exe` dans les [Releases GitHub](../../releases) (ou dans les artefacts de CI de n'importe quel run). Aucune installation, aucun droit administrateur, aucun runtime requis : posez le fichier où vous voulez (clé USB comprise) et double-cliquez. Windows 10/11 x64.

> **Premier lancement lent ?** C'est attendu pour un exécutable autonome de ~65 Mo téléchargé d'Internet : Windows le marque (« Mark of the Web »), SmartScreen interroge sa réputation en ligne et l'antivirus scanne l'intégralité du fichier ; l'application extrait aussi ses bibliothèques natives dans `%TEMP%` la première fois. Les lancements suivants sont bien plus rapides. Pour accélérer : clic droit → *Propriétés* → cochez **Débloquer** (ou `Unblock-File prompt-cleaner.exe` en PowerShell). L'exécutable n'étant pas signé numériquement, ces vérifications sont plus longues que pour un logiciel signé.

## Fonctionnement en bref

1. **Charger config…** : sélection via l'explorateur Windows d'un fichier de règles `motclé : remplacement` :
   ```
   google : mon-entreprise
   fb44ja8k : nom-user
   myApp : nom-application
   ```
   Le bouton « ? » à côté de « Charger config… » rappelle ce format dans l'application, avec un exemple copiable.
2. L'utilisateur colle son texte et clique sur **Clean**.
3. **Passe 1** : remplacement de toutes les occurrences des mots-clés du dictionnaire.
4. **Passe 2 (autonome)** : détection résiduelle — chemins `C:\Users\<nom>` (→ `XX_USER_XX`), e-mails, IP, téléphones, IBAN, cartes bancaires… remplacés ; GUID, secrets probables et URLs internes signalés en alerte.
5. Le résultat s'affiche avec un code couleur (légende incluse) et est **copié automatiquement dans le presse-papier** :
   - 🟩 **vert** : mot remplacé,
   - 🟥 **rouge** : donnée suspecte non remplacée, à vérifier manuellement.

Raccourcis clavier : `Alt+C` charger la config, `Alt+O` coller, `Alt+L` ou `Ctrl+Entrée` nettoyer, `Alt+P` copier.

## Variante web (zéro exécutable)

Si l'exécutable subit trop de friction antivirus/SmartScreen sur votre poste, la même application existe en **un seul fichier `web/index.html`** : téléchargez-le et double-cliquez — il s'ouvre dans votre navigateur, fonctionne 100 % hors-ligne (une CSP intégrée lui interdit tout appel réseau) et embarque le même moteur de nettoyage (dictionnaire + détecteurs D-01 à D-12). Le chargement de la config passe par le même explorateur de fichiers (ou un glisser-déposer), et une case « mémoriser dans ce navigateur » remplace l'auto-chargement du `.cfg`. Le bouton « ? » ouvre un **éditeur de règles intégré** : définissez ou modifiez vos règles directement dans la page et appliquez-les sans passer par un fichier.

Le nettoyage y est **automatique** : à un collage, le texte est nettoyé et le résultat copié dans le presse-papier immédiatement (un bref balayage lumineux du bandeau signale le scan) ; une saisie au clavier met le résultat à jour en direct, le bouton **Copier** (ou Ctrl+Entrée) servant alors à le placer dans le presse-papier. Le bouton **Coller** remplace le contenu existant (pas besoin de vider avant).

Un **historique de session** (bouton « Historique ») mémorise chaque traitement — texte d'entrée, sortie nettoyée et règles utilisées — et permet de les recharger. Il reste en mémoire uniquement (rien sur le disque) et disparaît à la fermeture de la page. Voir `docs/web-port.md` pour les détails et compromis.

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
| 3–4 | Détecteurs autonomes D-01 à D-12 | ✅ fait |
| 5 | Boucle presse-papier + rendu couleur | ✅ fait |
| 6 | Durcissement, accessibilité, release v1.0.0 | ✅ fait |

## Développement

```bash
dotnet build          # compile la solution (Windows, Linux ou macOS)
dotnet test           # exécute les tests unitaires du moteur

# Produit l'exécutable portable unique (publish/prompt-cleaner.exe) :
dotnet publish src/PromptCleaner.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -o publish
```

La CI GitHub Actions (`.github/workflows/ci.yml`) rejoue build + tests + publication à chaque push et attache l'exécutable en artefact ; un tag `v*` crée une Release GitHub avec l'exe en pièce jointe.

Des fichiers d'exemple sont fournis dans `samples/` (`regles-exemple.cfg`, `texte-exemple.txt`).

## Avertissement

prompt-cleaner est une aide à l'anonymisation : les alertes rouges exigent une vérification humaine, et l'absence d'alerte ne garantit pas l'absence de donnée sensible. Le presse-papier est écrasé à chaque nettoyage.
