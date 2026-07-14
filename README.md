# prompt-cleaner

Application web **100 % portable et hors-ligne**, en un seul fichier `web/index.html`, qui nettoie un texte de ses données personnelles/confidentielles et copie le résultat dans le presse-papier. Aucune installation, aucun serveur, aucun exécutable : elle s'ouvre par double-clic dans n'importe quel navigateur (Windows, macOS, Linux).

## Utilisation

1. Ouvrez `web/index.html` dans votre navigateur (double-clic, ou glisser-déposer dans un onglet).
2. Collez votre texte dans la zone d'entrée avec **Ctrl+V** — le nettoyage est **automatique** :
   - **Passe 1** : remplacement des mots-clés de votre dictionnaire de règles (facultatif) ;
   - **Passe 2 (autonome)** : détection résiduelle — chemins `C:\Users\<nom>` (→ `XX_USER_XX`), e-mails, IP, téléphones, IBAN, cartes bancaires… remplacés ; GUID, secrets probables et URLs internes signalés en alerte.
3. Le résultat s'affiche avec un code couleur (légende incluse) et est **copié automatiquement dans le presse-papier** :
   - 🟩 **vert** : mot remplacé,
   - 🟥 **rouge** : donnée suspecte non remplacée, à vérifier manuellement.

Le bouton **Vider** réinitialise la zone en un clic ; **Copier** (ou Ctrl+Entrée) replace le résultat dans le presse-papier après une saisie manuelle.

## Affiner l'anonymisation (facultatif)

Le nettoyage automatique fonctionne sans configuration. Pour aller plus loin, le panneau latéral permet de fournir un dictionnaire de règles `motclé : remplacement` :

```
google : mon-entreprise
fb44ja8k : nom-user
myApp : nom-application
```

Deux façons de le faire : **charger un fichier** `.cfg`/`.txt` (bouton « Charger config… » ou glisser-déposer), ou **éditer les règles directement dans la page** via le bouton « ? » (éditeur intégré, applicable sans fichier). Une case « mémoriser dans ce navigateur » conserve les règles d'une visite à l'autre (stockage local, jamais envoyé en ligne).

## Historique de session

Le bouton « Historique » mémorise chaque traitement — texte d'entrée, sortie nettoyée et règles utilisées — et permet de les recharger. Il reste **en mémoire uniquement** (rien n'est écrit sur le disque) et disparaît à la fermeture ou au rechargement de la page.

## Confidentialité

L'application est **hors-ligne par construction** : une politique de sécurité de contenu (CSP `default-src 'none'`) intégrée au fichier lui interdit tout appel réseau — vérifiable en ouvrant `web/index.html`. Aucune donnée ne quitte le navigateur, rien n'est écrit sur le disque.

> **Note sur le bouton de collage** : les navigateurs interdisent à une page ouverte en fichier local (`file://`) de lire le presse-papier par programme — le collage se fait donc au clavier avec **Ctrl+V** (`Ctrl+A` puis `Ctrl+V` pour remplacer tout le texte). Si la page est servie en https (voir ci-dessous), cette restriction disparaît.

## Développement

Le moteur de nettoyage (JavaScript) est délimité dans `web/index.html` par les marqueurs `/*ENGINE-START*/ … /*ENGINE-END*/`. La suite de tests `web/test.mjs` (Node, sans dépendance) l'extrait et le soumet à une batterie de cas :

```bash
node web/test.mjs
```

La CI GitHub Actions (`.github/workflows/ci.yml`) rejoue ces tests à chaque push. Des fichiers d'exemple sont fournis dans `samples/` (`regles-exemple.cfg`, `texte-exemple.txt`).

## Documentation

- [`docs/brief.md`](docs/brief.md) — cadrage du besoin (Project Brief).
- [`docs/prd.md`](docs/prd.md) — exigences fonctionnelles, détecteurs D-01 à D-12, UX.
- [`docs/web-port.md`](docs/web-port.md) — conception de l'application web, comportements et compromis.

## Avertissement

prompt-cleaner est une aide à l'anonymisation : les alertes rouges exigent une vérification humaine, et l'absence d'alerte ne garantit pas l'absence de donnée sensible. Le presse-papier est écrasé à chaque nettoyage.
