# Backlog — prompt-cleaner

> **Méthode BMAD — Phase 4 : Product Owner / Scrum Master**
> Le PO a validé la cohérence Brief ↔ PRD ↔ Architecture ; le SM découpe ici les épics du PRD en user stories prêtes pour le développement (cycle Dev → QA de BMAD : une story à la fois, critères d'acceptation vérifiables).

## Definition of Done (toutes stories)

- Code mergé sur `main` via PR, CI verte (build + tests).
- Logique métier couverte par des tests xUnit dans `PromptCleaner.Core.Tests`.
- Aucune écriture disque ni appel réseau introduits.
- Critères d'acceptation de la story démontrés (test auto ou procédure manuelle notée dans la PR).

---

## Épic E1 — Socle applicatif portable

### S1.1 — Squelette de solution
**En tant que** développeur, **je veux** une solution .NET 8 avec les projets `Core`, `App` (WinForms) et `Core.Tests`, **afin de** poser la structure cible.
**CA :**
1. `dotnet build` et `dotnet test` passent sur un clone frais.
2. `App` référence `Core` ; `Core` ne référence aucun paquet UI.
3. Un test xUnit d'exemple s'exécute.

### S1.2 — Fenêtre principale statique
**En tant qu'** utilisateur, **je veux** la fenêtre avec zones d'entrée/résultat, boutons (Charger config, Coller, Clean, Copier), légende et barre d'état, **afin de** disposer de l'ossature UI (sans logique).
**CA :**
1. La fenêtre correspond à la maquette du PRD §4 ; redimensionnable, tailles minimales définies.
2. La légende verte/rouge est visible en permanence.
3. Le bouton Clean est désactivé quand la zone d'entrée est vide.

### S1.3 — Publication portable + CI
**En tant qu'** utilisateur final, **je veux** un `prompt-cleaner.exe` unique auto-suffisant produit par la CI, **afin de** l'exécuter sans installation.
**CA :**
1. `dotnet publish` (profil ADR §8) produit un seul `.exe` ; il démarre sur Windows sans .NET installé.
2. Workflow GitHub Actions : build + tests sur push/PR ; exe en artefact ; Release sur tag `v*`.
3. Aucune écriture registre ; exécution vérifiée depuis un dossier en lecture seule → message d'erreur propre le cas échéant (pas de crash).

---

## Épic E2 — Dictionnaire de configuration

### S2.1 — Parseur du fichier de règles
**En tant qu'** utilisateur, **je veux** que mon fichier `clé : valeur` soit transformé en règles de remplacement, **afin de** définir mes propres substitutions.
**CA :**
1. `google : mon-entreprise`, `fb44ja8k:nom-user`, `myApp : nom-application` → 3 règles correctes (espaces tolérés).
2. Lignes vides et lignes `#commentaire` ignorées silencieusement ; lignes invalides comptées et remontées (FR-1.4).
3. UTF-8 avec/sans BOM ; doublon de clé → la dernière ligne gagne, signalé dans le rapport de chargement.
4. Couvert par des tests unitaires exhaustifs (fichier vide, clé vide, valeur vide, `:` multiples — la 1re occurrence sépare).

### S2.2 — Chargement via l'explorateur Windows
**En tant qu'** utilisateur, **je veux** choisir mon fichier de config via la boîte de dialogue standard, **afin de** le charger sans saisir de chemin.
**CA :**
1. **Charger config…** ouvre `OpenFileDialog` (filtres `*.cfg;*.txt` + `*.*`).
2. Après chargement : chemin + nombre de règles affichés (« regles.cfg — 12 règles, 1 ligne ignorée »).
3. Fichier illisible/verrouillé → message d'erreur clair, état précédent conservé.

### S2.3 — Auto-chargement portable
**En tant qu'** utilisateur nomade, **je veux** que `prompt-cleaner.cfg` posé à côté de l'exe soit chargé au démarrage, **afin d'** utiliser l'outil sur clé USB sans manipulation.
**CA :**
1. Fichier présent → chargé au lancement, signalé dans la barre d'état.
2. Absent → application utilisable, avertissement « aucune config chargée : seule la détection automatique s'appliquera ».
3. Résolution via `AppContext.BaseDirectory` (fonctionne en single-file).

---

## Épic E3 — Moteur : passe dictionnaire

### S3.1 — Remplacement multi-occurrences avec spans
**En tant qu'** utilisateur, **je veux** que toutes les occurrences de mes mots-clés soient remplacées, **afin d'** anonymiser mes termes connus.
**CA :**
1. Toutes les occurrences remplacées, insensible à la casse, valeur de remplacement littérale.
2. Mots-clés longs prioritaires : avec `app`→`X` et `myApp`→`Y`, le texte `myApp` donne `Y` (pas `myX`).
3. Chaque remplacement produit un span `Replaced` avec offsets exacts dans le texte final (vérifié par tests).
4. Une zone remplacée n'est pas re-matchée par une autre règle (FR-3.3).
5. 1 Mo de texte + 100 règles < 1 s (test de performance).

---

## Épic E4 — Moteur : passe autonome

### S4.1 — Cadre des détecteurs + jetons déterministes
**En tant que** développeur, **je veux** l'interface `IDetector`, l'orchestration ordonnée et le générateur de jetons (`XX_TYPE_XX`, numérotation multi-valeurs), **afin de** brancher les détecteurs uniformément.
**CA :**
1. Les détecteurs s'exécutent dans l'ordre D-01→D-12 et ignorent les zones déjà couvertes.
2. Même valeur ⇒ même jeton ; valeurs distinctes du même type ⇒ `XX_EMAIL_1_XX`, `XX_EMAIL_2_XX`.
3. Timeout regex 100 ms géré sans crash (le détecteur fautif est ignoré + alerte dans la barre d'état).

### S4.2 — D-01/D-02/D-12 : chemins Windows, UNC et écho du nom d'utilisateur
**CA :**
1. `C:\Users\jdupont\Documents` → `C:\Users\XX_USER_XX\Documents` (reste du chemin intact), idem `c:\users\` et `D:\Users\`.
2. `\\SRV-PARIS\commun\doc.txt` → `\\XX_HOST_XX\commun\doc.txt`.
3. Après D-01, l'occurrence isolée « jdupont » ailleurs dans le texte devient `XX_USER_XX` (D-12) ; « jdupont2 » n'est pas touché (frontières de mots).

### S4.3 — D-03/D-04/D-05 : e-mails, IPv4, téléphones FR
**CA :**
1. `paul.dupont@societe.fr` → `XX_EMAIL_1_XX` ; deux e-mails distincts → jetons distincts.
2. `192.168.1.42` remplacé ; `127.0.0.1` et `192.0.2.1` (plage doc) laissés intacts ; `999.1.1.1` non matché.
3. `06 12 34 56 78`, `06.12.34.56.78`, `+33 6 12 34 56 78` → `XX_TEL_XX` ; un numéro de version `1.2.3.4.5` non matché.

### S4.4 — D-06/D-07/D-08 : IBAN, NIR, carte bancaire
**CA :**
1. IBAN FR valide (avec/sans espaces) remplacé ; chaîne ressemblante à checksum invalide → non remplacée.
2. NIR 15 chiffres (avec/sans espaces) remplacé.
3. Numéro CB validé Luhn remplacé ; suite de 16 chiffres échouant Luhn → non remplacée.

### S4.5 — D-09/D-10/D-11 : alertes GUID, secrets, URLs internes
**CA :**
1. GUID et jetons à préfixe connu (`sk-`, `ghp_`, `AKIA`, `Bearer …`) → span `Alert` (rouge), texte **inchangé**.
2. Chaîne ≥ 20 caractères mixtes à entropie > seuil → `Alert` ; un hash SHA hexadécimal pur documenté comme cas limite accepté.
3. URL avec domaine non générique (`intranet.societe.fr`) → `Alert` ; `github.com`, `stackoverflow.com`, `google.com` (liste blanche) → ignorées.

---

## Épic E5 — Restitution : couleurs, légende, presse-papier

### S5.1 — Rendu coloré du résultat
**CA :**
1. Spans `Replaced` en vert (fond + teinte), `Alert` en rouge (fond + teinte) dans la `RichTextBox` en lecture seule.
2. Positions couleur exactes y compris avec accents/emoji (offsets .NET UTF-16 vérifiés).
3. Coloration d'un résultat de 1 Mo sans gel perceptible (suspension du redraw).

### S5.2 — Presse-papier entrée/sortie
**CA :**
1. Fin de Clean → texte **brut** nettoyé dans le presse-papier, barre d'état « copié ✔ » (FR-6.1).
2. **Coller** remplace l'entrée par le presse-papier ; presse-papier vide/non-texte → message doux, pas de crash.
3. **Copier** recopie le résultat courant.

### S5.3 — Statistiques et statut
**CA :**
1. Barre d'état après Clean : « N remplacements (a config, b auto) — c alertes ».
2. Zéro donnée détectée → message explicite « aucune donnée sensible détectée » (pas une barre vide).
3. Traitement > 100 Ko : exécution asynchrone, bouton Clean désactivé pendant le calcul, curseur d'attente.

---

## Épic E6 — Durcissement & livraison

### S6.1 — Robustesse et cas limites
**CA :**
1. Campagne de tests : texte vide, 5 Mo, uniquement des emoji, fins de ligne mixtes CRLF/LF, config de 10 000 règles.
2. Aucune exception non gérée (handler global → boîte de dialogue d'erreur + poursuite).

### S6.2 — Accessibilité et finitions UI
**CA :**
1. Contrastes des surlignages conformes (vérif WCAG AA sur fond/texte), distinction non limitée à la teinte (NFR-6).
2. Navigation clavier complète (tab order, Alt+raccourcis boutons, Ctrl+Entrée = Clean).

### S6.3 — Documentation & release v1.0.0
**CA :**
1. README : téléchargement, exemple de `.cfg`, capture d'écran, avertissement « relecture humaine requise » et « le presse-papier est écrasé ».
2. `samples/regles-exemple.cfg` + `samples/texte-exemple.txt` démontrent chaque détecteur.
3. Tag `v1.0.0` → Release GitHub avec `prompt-cleaner.exe` attaché, testé sur machine Windows vierge.

---

## Ordonnancement (cycle Dev/QA BMAD — une story à la fois)

| Itération | Stories | Jalon démontrable |
|---|---|---|
| **It. 1** | S1.1 → S1.2 → S1.3 | L'exe portable s'ouvre ; CI verte |
| **It. 2** | S2.1 → S2.2 → S2.3 → S3.1 | Clean remplace les mots du dictionnaire (encore sans couleurs) |
| **It. 3** | S4.1 → S4.2 → S4.3 | Chemins Windows, e-mails, IP, téléphones nettoyés automatiquement |
| **It. 4** | S4.4 → S4.5 → S5.1 | Détection complète + rendu vert/rouge avec légende |
| **It. 5** | S5.2 → S5.3 | Boucle presse-papier complète = **MVP fonctionnel de bout en bout** |
| **It. 6** | S6.1 → S6.2 → S6.3 | **Release v1.0.0 portable** |

Priorité de valeur : après l'itération 2 l'outil est déjà utilisable (dictionnaire + copie) ; chaque itération suivante ajoute une couche de sécurité ou de confort sans casser la précédente.
