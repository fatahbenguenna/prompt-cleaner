# Project Brief — prompt-cleaner

> **Méthode BMAD — Phase 1 : Analyste (Business Analysis)**
> Document produit par le rôle *Analyst*. Il cadre le problème, le marché cible et les contraintes avant la rédaction du PRD.

## 1. Résumé exécutif

**prompt-cleaner** est une application Windows fenêtrée, **100 % portable (un seul `.exe`, aucune installation, aucun droit administrateur)**, qui nettoie un texte de ses données personnelles ou confidentielles avant qu'il ne soit collé dans un outil tiers (IA générative, ticket de support, forum, e-mail…).

L'utilisateur colle un texte, clique sur **Clean**, et récupère automatiquement dans le presse-papier une version anonymisée. La fenêtre affiche le résultat avec un code couleur : **vert** pour les mots remplacés, **rouge** pour les données sensibles détectées mais non remplacées (alerte).

## 2. Problème à résoudre

Les utilisateurs (développeurs, support IT, employés) copient quotidiennement des logs, extraits de code, e-mails ou chemins de fichiers dans des assistants IA ou des outils externes. Ces textes contiennent souvent :

- des noms d'entreprise, de projets internes, d'utilisateurs (`C:\Users\jdupont\...`) ;
- des identifiants techniques (clés API, GUID, adresses IP, e-mails) ;
- des données réglementées (RGPD) : e-mails, téléphones, IBAN, NIR.

Il n'existe pas d'outil simple, hors-ligne et portable pour anonymiser ce texte en un clic, avec un dictionnaire de remplacement propre à chaque utilisateur/entreprise.

## 3. Utilisateurs cibles

| Persona | Besoin principal |
|---|---|
| Développeur / DevOps | Coller des logs et du code dans un LLM sans fuiter noms de serveurs, users, clés |
| Support / Helpdesk | Partager des captures de configuration sans exposer les clients |
| Employé en entreprise verrouillée | Outil **sans installation** (pas de droits admin, exécutable depuis une clé USB ou un partage réseau) |

## 4. Proposition de valeur

1. **Zéro installation** : un seul fichier `.exe` autonome, aucun runtime à installer, aucune écriture dans le registre.
2. **Hors-ligne par conception** : aucune donnée ne quitte la machine (argument de confiance essentiel pour un outil d'anonymisation).
3. **Dictionnaire personnel** : fichier de configuration `motclé : remplacement` choisi via l'explorateur Windows.
4. **Filet de sécurité autonome** : détecteurs intégrés (chemins Windows, e-mails, IP, clés…) qui remplacent ou alertent sur ce que le dictionnaire n'a pas couvert.
5. **Feedback visuel immédiat** : vert = remplacé, rouge = à vérifier, avec légende.

## 5. Périmètre

### Inclus (MVP)
- Fenêtre unique : zone d'entrée, bouton **Clean**, zone de résultat colorée, légende, barre d'état.
- Chargement d'un fichier de configuration via boîte de dialogue Windows (`OpenFileDialog`).
- Remplacement de toutes les occurrences des mots-clés du dictionnaire.
- Passe autonome de détection/remplacement/alerte (chemins `C:\Users\<nom>`, e-mails, IP, etc.).
- Copie automatique du résultat nettoyé dans le presse-papier.
- Collage depuis le presse-papier (bouton dédié + Ctrl+V standard).

### Exclus (hors MVP, pistes futures)
- Édition du dictionnaire dans l'application (v2).
- Historique / journal des nettoyages (volontairement exclu : ne rien persister).
- Version macOS/Linux, intégration au menu contextuel Windows, mode CLI, surveillance continue du presse-papier.

## 6. Contraintes

| Contrainte | Détail |
|---|---|
| **Portabilité** | Un seul `.exe`, exécutable sans installation ni droits admin, y compris depuis une clé USB |
| **Plateforme** | Windows 10/11 x64 (référence explicite aux dialogues et chemins Windows dans le besoin) |
| **Confidentialité** | Aucun appel réseau, aucune télémétrie, aucune persistance du texte traité |
| **Simplicité** | Utilisable sans documentation : coller → Clean → résultat dans le presse-papier |
| **Performance** | Nettoyage instantané (< 1 s) pour des textes de l'ordre de 1 Mo |

## 7. Critères de succès

- L'exécutable démarre sur un poste Windows vierge (sans .NET/Python/etc. préinstallé) en double-cliquant.
- Un texte contenant les mots-clés du fichier de config exemple est nettoyé à 100 % (vert).
- Un chemin `C:\Users\jdupont\Documents` est automatiquement transformé en `C:\Users\XX_USER_XX\Documents` sans configuration.
- Le résultat est dans le presse-papier sans action supplémentaire après le clic sur **Clean**.

## 8. Risques identifiés

| Risque | Impact | Mitigation |
|---|---|---|
| Faux positifs des détecteurs autonomes (mots légitimes marqués en rouge) | Moyen | Le rouge est une **alerte**, pas un remplacement ; règles conservatrices ; seuils réglables dans le code |
| Faux négatifs (donnée sensible non détectée) | Élevé | Communication claire : l'outil est une aide, la relecture humaine reste requise ; légende explicite |
| Antivirus signalant un exécutable non signé | Moyen | Publication self-contained standard .NET (bien connue des AV), option de signature de code ultérieure |
| Taille de l'exécutable self-contained (~60–80 Mo) | Faible | Acceptable pour un outil poste de travail ; compression activée ; alternative « framework-dependent » documentée |
