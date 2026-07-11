# Portage web — prompt-cleaner en un seul fichier `index.html`

> Addendum aux documents BMAD (`brief.md`, `prd.md`, `architecture.md`).
> Décision prise après retour terrain sur la v1 WinForms.

## 1. Motivation

L'exécutable autonome (~64 Mo, non signé) subit une friction importante sur
les postes Windows : Mark of the Web, vérification SmartScreen en ligne,
scan antivirus complet à chaque accès. Sur un poste d'entreprise verrouillé,
cette friction peut rendre l'outil inutilisable en pratique — alors que tout
le cœur de prompt-cleaner est du traitement de texte pur, parfaitement
portable en JavaScript.

## 2. Décision

> **Un fichier `web/index.html` unique et auto-suffisant** : HTML + CSS + JS
> inline, aucune dépendance, aucun build, aucune ressource externe.
> Il s'ouvre par double-clic dans n'importe quel navigateur (Windows, macOS,
> Linux), depuis le disque, une clé USB ou un partage réseau.

Garanties :

- **Hors-ligne par construction** : une balise `Content-Security-Policy`
  (`default-src 'none'`) interdit au fichier tout appel réseau — vérifiable
  en lisant l'en-tête du fichier. Rien ne quitte le navigateur.
- **Zéro friction antivirus** : un fichier HTML de quelques dizaines de Ko
  n'est ni scanné longuement, ni soumis à la réputation SmartScreen des
  exécutables.
- **Moteur iso-fonctionnel** : portage 1:1 de `PromptCleaner.Core` — passe
  dictionnaire (mots-clés longs d'abord, casse ignorée, pas de cascade),
  détecteurs D-01 → D-12, jetons stables `XX_TYPE_XX` numérotés, alertes.

## 3. Correspondance fonctionnelle

| Fonction WinForms | Équivalent web |
|---|---|
| `OpenFileDialog` pour la config | `<input type="file">` (même explorateur natif) + glisser-déposer sur la page |
| Auto-chargement de `prompt-cleaner.cfg` à côté de l'exe | Impossible depuis un navigateur → case « mémoriser dans ce navigateur » (localStorage, opt-in) |
| Bouton Clean | supprimé : nettoyage **automatique** (voir §4bis) |
| Copie auto dans le presse-papier | `navigator.clipboard.writeText` + repli `execCommand("copy")` |
| Bouton Coller | **remplace** le contenu (vide + colle) via `clipboard.readText` ; si refusé, sélectionne tout pour qu'un Ctrl+V remplace au lieu d'ajouter |
| RichTextBox verte/rouge + légende | `<span class="replaced|alert">` (mêmes couleurs, contrastes AA conservés) + légende |
| Barre d'état | pied de page de statut |
| Popin « ? » format de config | `<dialog>` natif devenu **éditeur de règles intégré** : pré-rempli avec la config courante (ou l'exemple), compteur de règles en direct, « Utiliser ces règles » applique le contenu sans fichier, « Copier » permet d'en faire un `.cfg` |

## 4bis. Nettoyage automatique (variante web)

Le bouton « Clean » disparaît au profit d'un nettoyage déclenché tout seul,
contraint par une règle des navigateurs : **le presse-papier n'est accessible
en écriture que pendant un geste utilisateur**.

| Déclencheur | Nettoyage | Copie presse-papier | Animation de scan |
|---|---|---|---|
| Collage (Ctrl+V, clic droit, bouton Coller) | oui | oui (geste présent) | oui |
| Saisie / édition au clavier | oui (aperçu live, anti-rebond 300 ms) | non — bouton Copier / Ctrl+Entrée | non |
| Chargement d'un fichier ou application du template (si texte présent) | oui | tentée | oui |
| Ctrl+Entrée | oui | oui | oui |

Le liseré de scan du bandeau ne s'affiche donc **que le temps d'un nettoyage
déclenché** (~0,85 s), et non plus en boucle continue.

## 4ter. Historique de session

Chaque nettoyage abouti (collage, application de règles, Ctrl+Entrée, ou sortie
de la zone d'entrée après une saisie) est mémorisé **en mémoire uniquement** —
jamais écrit sur le disque : l'historique disparaît à la fermeture ou au
rechargement de la page. Ce choix est volontaire, l'outil manipulant des données
sensibles.

Chaque entrée conserve : le texte d'entrée, le texte de sortie nettoyé, les
statistiques (remplacements/alertes) et un instantané des règles utilisées
(nom + contenu). Le bouton « Historique (N) » ouvre une liste (les 50 derniers,
plus récents d'abord) où chaque traitement peut être **rechargé** (texte + règles
d'origine restaurés, résultat régénéré) ou dont la **sortie peut être recopiée**.
Un bouton « Vider l'historique » remet le compteur à zéro. Les doublons consécutifs
(même entrée + mêmes règles) ne sont pas ré-enregistrés.

## 4. Compromis assumés

- La lecture du presse-papier par bouton dépend des permissions du
  navigateur sur les pages locales ; le collage `Ctrl+V` fonctionne toujours.
- Pas de fenêtre applicative dédiée : c'est un onglet de navigateur.
- Les très gros textes (> quelques Mo) sont traités de façon synchrone :
  acceptable pour l'usage visé (prompts, logs).

## 5. Tests

Le moteur JavaScript est délimité dans `index.html` par des marqueurs
`/*ENGINE-START*/ … /*ENGINE-END*/`. La suite `web/test.mjs` (Node, sans
dépendance) extrait ce code tel quel et lui applique les mêmes critères
d'acceptation que la suite xUnit du moteur C# (32 tests). La CI l'exécute
à chaque push (job « Tests du portage web »).

## 6. Distribution

- **Local** : télécharger `web/index.html`, double-cliquer. C'est tout.
- **Hébergé** (option future) : le même fichier peut être servi tel quel
  (GitHub Pages, intranet) — la CSP continue de garantir qu'aucune donnée
  n'est envoyée nulle part.

Les deux variantes (WinForms et web) partagent la même spécification
fonctionnelle et coexistent dans le dépôt.
