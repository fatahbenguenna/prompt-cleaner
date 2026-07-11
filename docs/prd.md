# PRD — prompt-cleaner

> **Méthode BMAD — Phase 2 : Product Manager (PRD)**
> Document produit par le rôle *PM* à partir du Project Brief (`docs/brief.md`).
> Il définit les exigences fonctionnelles (FR), non fonctionnelles (NFR), l'UX et découpe le produit en épics.

## 1. Objectif du produit

Fournir une application Windows portable qui, en un clic, remplace dans un texte toutes les données personnelles/confidentielles connues (dictionnaire utilisateur) et détectées (heuristiques intégrées), copie le résultat dans le presse-papier et affiche le texte nettoyé avec un code couleur vert/rouge.

## 2. Exigences fonctionnelles (FR)

### FR-1 — Fichier de configuration (dictionnaire de remplacement)
- **FR-1.1** L'utilisateur charge un fichier de configuration via un bouton **Charger config…** ouvrant la boîte de dialogue standard de l'explorateur Windows (`OpenFileDialog`).
- **FR-1.2** Format du fichier : texte UTF-8, une règle par ligne, `motclé : remplacement`.
  ```
  # Commentaire (ligne ignorée, comme les lignes vides)
  google : mon-entreprise
  fb44ja8k : nom-user
  myApp : nom-application
  ```
- **FR-1.3** Les espaces autour de `:` sont tolérés. La casse des mots-clés est **insensible** par défaut (`Google` = `google`), le remplacement conserve la valeur de droite telle quelle.
- **FR-1.4** Une ligne invalide n'interrompt pas le chargement : elle est comptée et signalée dans la barre d'état (« 12 règles chargées, 1 ligne ignorée »).
- **FR-1.5** Le chemin du dernier fichier chargé est affiché dans l'interface. L'application fonctionne aussi **sans** config (seule la passe autonome s'applique) avec un avertissement visible.
- **FR-1.6** Si un fichier `prompt-cleaner.cfg` est présent à côté de l'exécutable, il est chargé automatiquement au démarrage (confort portable, ex. clé USB).

### FR-2 — Saisie du texte
- **FR-2.1** Zone d'entrée multi-lignes acceptant le collage standard (Ctrl+V, clic droit).
- **FR-2.2** Bouton **Coller** qui remplace le contenu de la zone d'entrée par le presse-papier.
- **FR-2.3** Textes d'au moins 1 Mo acceptés sans blocage de l'interface.

### FR-3 — Nettoyage (bouton **Clean**)
- **FR-3.1** Le clic sur **Clean** déclenche le pipeline : *Passe 1 (dictionnaire)* puis *Passe 2 (autonome)*.
- **FR-3.2** **Passe 1** : toutes les occurrences de chaque mot-clé du dictionnaire sont remplacées. Les mots-clés les plus longs sont appliqués en premier (évite qu'un mot-clé court casse un mot-clé long). Chaque remplacement est marqué **vert**.
- **FR-3.3** Les zones déjà remplacées ne sont pas re-analysées par les règles suivantes (pas de remplacement en cascade).

### FR-4 — Passe autonome (détection résiduelle)
Après la passe dictionnaire, le texte restant est analysé par des détecteurs intégrés. Deux actions possibles par détecteur :
- **REMPLACER** (résultat en **vert**) quand le remplacement est sûr et sans perte de sens ;
- **ALERTER** (texte marqué **rouge**, non modifié) quand un remplacement automatique risquerait de casser le texte ou de sur-anonymiser.

| ID | Détecteur | Exemple | Action par défaut | Jeton |
|---|---|---|---|---|
| D-01 | Nom d'utilisateur dans chemin Windows `C:\Users\<nom>\` (et `C:\Documents and Settings\`) | `C:\Users\jdupont\AppData` | REMPLACER le nom | `XX_USER_XX` |
| D-02 | Chemin UNC `\\serveur\partage` | `\\SRV-PARIS\commun` | REMPLACER serveur | `XX_HOST_XX` |
| D-03 | Adresse e-mail | `paul.dupont@societe.fr` | REMPLACER | `XX_EMAIL_XX` |
| D-04 | Adresse IPv4 (hors plages doc/localhost) | `192.168.1.42` | REMPLACER | `XX_IP_XX` |
| D-05 | Numéro de téléphone FR (`0x xx xx xx xx`, `+33…`) | `06 12 34 56 78` | REMPLACER | `XX_TEL_XX` |
| D-06 | IBAN | `FR76 3000 6000 0112…` | REMPLACER | `XX_IBAN_XX` |
| D-07 | NIR (sécurité sociale FR, 15 chiffres) | `1 85 05 78 006 084 36` | REMPLACER | `XX_NIR_XX` |
| D-08 | Numéro de carte bancaire (validation Luhn) | `4970 1234 5678 9010` | REMPLACER | `XX_CB_XX` |
| D-09 | GUID/UUID | `550e8400-e29b-41d4-a716…` | ALERTER | — |
| D-10 | Secret probable : chaîne ≥ 20 caractères à forte entropie, ou préfixes connus (`sk-`, `ghp_`, `AKIA`, `Bearer `…) | `sk-abc123…` | ALERTER | — |
| D-11 | URL avec nom de domaine non générique | `https://intranet.societe.fr/page` | ALERTER | — |
| D-12 | Variable d'environnement utilisateur (`%USERNAME%`, `%USERPROFILE%` résolus) | `jdupont` isolé après D-01 | ALERTER les autres occurrences du nom détecté en D-01 | — |

- **FR-4.1** Chaque type de donnée reçoit un jeton stable et distinct (`XX_USER_XX`, `XX_EMAIL_XX`…). Si plusieurs valeurs distinctes du même type apparaissent, elles sont numérotées (`XX_EMAIL_1_XX`, `XX_EMAIL_2_XX`) afin de préserver la cohérence du texte.
- **FR-4.2** La même valeur source donne toujours le même jeton dans un même nettoyage (mapping déterministe).
- **FR-4.3** D-12 : quand D-01 extrait un nom d'utilisateur d'un chemin, **toutes** les autres occurrences isolées de ce nom dans le texte sont remplacées par le même jeton `XX_USER_XX` (vert).

### FR-5 — Affichage du résultat
- **FR-5.1** Le texte nettoyé s'affiche dans une zone résultat en lecture seule.
- **FR-5.2** Les segments remplacés (passes 1 et 2) sont en **vert**. Les segments en alerte (détectés mais non remplacés) sont en **rouge**.
- **FR-5.3** Une **légende** permanente est affichée sous la zone résultat : `■ vert : remplacé — ■ rouge : donnée suspecte non remplacée (à vérifier)`.
- **FR-5.4** La barre d'état affiche un récapitulatif : « 14 remplacements (8 dictionnaire, 6 auto), 2 alertes ».

### FR-6 — Presse-papier
- **FR-6.1** Dès la fin du nettoyage, le **texte brut nettoyé** (sans balisage couleur) est copié automatiquement dans le presse-papier.
- **FR-6.2** Un bouton **Copier** permet de recopier le résultat à tout moment.
- **FR-6.3** L'application n'écrit jamais le texte (entrée ou sortie) sur le disque.

## 3. Exigences non fonctionnelles (NFR)

| ID | Exigence |
|---|---|
| NFR-1 | **Portabilité** : un unique `.exe` autonome, aucun installateur, aucun prérequis runtime, aucune écriture registre ; exécutable depuis clé USB/partage réseau sans droits admin |
| NFR-2 | **Plateforme** : Windows 10 (1809+) et Windows 11, x64 |
| NFR-3 | **Hors-ligne** : aucun appel réseau, aucune télémétrie |
| NFR-4 | **Performance** : nettoyage < 1 s pour 100 Ko, < 5 s pour 1 Mo ; interface non bloquée (traitement asynchrone au-delà de 100 Ko) |
| NFR-5 | **Robustesse** : fichier de config corrompu, presse-papier vide ou non-texte → message clair, jamais de crash |
| NFR-6 | **Accessibilité couleur** : les segments verts/rouges sont aussi distingués par le fond surligné (pas uniquement la teinte) pour les daltoniens |
| NFR-7 | **Langue** : interface en français (MVP) ; chaînes isolées pour i18n future |
| NFR-8 | **Qualité** : moteur de nettoyage couvert par des tests unitaires (≥ 90 % sur `Core`) ; CI GitHub Actions build + tests + publication de l'exe en artefact |

## 4. UX — Maquette de la fenêtre unique

```
┌───────────────────────────────────────────────────────────────────┐
│ prompt-cleaner                                          [_][□][X] │
├───────────────────────────────────────────────────────────────────┤
│ [ Charger config… ]  config : C:\...\regles.cfg (12 règles)       │
├───────────────────────────────────────────────────────────────────┤
│ Texte d'entrée                                     [ Coller ]     │
│ ┌───────────────────────────────────────────────────────────────┐ │
│ │ (zone de texte multi-lignes)                                  │ │
│ └───────────────────────────────────────────────────────────────┘ │
│                        [   🧹 Clean   ]                           │
│ Résultat (copié dans le presse-papier)             [ Copier ]     │
│ ┌───────────────────────────────────────────────────────────────┐ │
│ │ Bonjour, je travaille chez mon-entreprise sur nom-application.│ │
│ │ Mon dossier est C:\Users\XX_USER_XX\src et ma clé sk-ab12… ⚠  │ │
│ └───────────────────────────────────────────────────────────────┘ │
│ Légende : ■ vert = remplacé   ■ rouge = suspect non remplacé      │
├───────────────────────────────────────────────────────────────────┤
│ 14 remplacements (8 config, 6 auto) — 2 alertes — copié ✔         │
└───────────────────────────────────────────────────────────────────┘
```

## 5. Découpage en épics

| Épic | Titre | Contenu | FR couvertes |
|---|---|---|---|
| **E1** | Socle applicatif portable | Solution .NET, fenêtre WinForms, publication single-file, CI | NFR-1/2/8 |
| **E2** | Dictionnaire de configuration | Parseur du fichier de règles, dialogue d'ouverture, auto-chargement | FR-1 |
| **E3** | Moteur de nettoyage — passe dictionnaire | Remplacement multi-occurrences avec spans traçables | FR-3 |
| **E4** | Moteur de nettoyage — passe autonome | Détecteurs D-01 à D-12, jetons, mapping déterministe | FR-4 |
| **E5** | Restitution : couleurs, légende, presse-papier | RichTextBox colorée, légende, copie auto, barre d'état | FR-2, FR-5, FR-6 |
| **E6** | Durcissement & livraison | Robustesse, performance, accessibilité, doc utilisateur, release GitHub | NFR-3→7 |

Le détail des user stories, critères d'acceptation et l'ordonnancement se trouvent dans `docs/backlog.md` (Phase Scrum Master).

## 6. Hors périmètre (rappel)

Édition du dictionnaire dans l'UI, historique, multi-plateforme, CLI, surveillance continue du presse-papier, signature de code — consignés comme candidats v2.
