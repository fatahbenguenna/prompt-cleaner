// Tests du portage web : le moteur est extrait tel quel de index.html
// (entre les marqueurs ENGINE-START/END) puis soumis aux mêmes critères
// d'acceptation que PromptCleaner.Core (xUnit).
//
// Exécution : node web/test.mjs

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const html = readFileSync(join(here, "index.html"), "utf-8");

const START = "/*ENGINE-START*/";
const END = "/*ENGINE-END*/";
const engineSource = html.slice(html.indexOf(START) + START.length, html.indexOf(END));
if (engineSource.length < 100) {
  console.error("Impossible d'extraire le moteur de index.html (marqueurs absents ?)");
  process.exit(1);
}

const PromptCleaner = new Function(`${engineSource}; return PromptCleaner;`)();
const { parseConfig, clean } = PromptCleaner;

// ----- Mini harnais ---------------------------------------------------------

let passed = 0;
const failures = [];

function test(name, fn) {
  try {
    fn();
    passed++;
  } catch (error) {
    failures.push({ name, error });
  }
}

function assertEqual(actual, expected, label = "") {
  const a = JSON.stringify(actual);
  const e = JSON.stringify(expected);
  if (a !== e) throw new Error(`${label}\n  attendu : ${e}\n  obtenu  : ${a}`);
}

const cleanText = (text, rules = []) => clean(text, rules).cleanText;

// ----- Fichier de configuration (S2.1) --------------------------------------

test("config : exemple du besoin → 3 règles", () => {
  const r = parseConfig("google : mon-entreprise\nfb44ja8k:nom-user\nmyApp : nom-application");
  assertEqual(r.rules, [
    { keyword: "google", replacement: "mon-entreprise" },
    { keyword: "fb44ja8k", replacement: "nom-user" },
    { keyword: "myApp", replacement: "nom-application" },
  ]);
  assertEqual(r.ignoredLineCount, 0);
});

test("config : commentaires et lignes vides ignorés sans être comptés", () => {
  const r = parseConfig("\n# commentaire\n\n  # autre\ngoogle : x\n\n");
  assertEqual(r.rules.length, 1);
  assertEqual(r.ignoredLineCount, 0);
});

test("config : lignes invalides comptées sans interrompre", () => {
  for (const invalid of ["pas-de-separateur", ": valeur", "cle :", ":"]) {
    const r = parseConfig(`google : x\n${invalid}\nmyApp : y`);
    assertEqual(r.rules.length, 2, invalid);
    assertEqual(r.ignoredLineCount, 1, invalid);
  }
});

test("config : le premier « : » sépare, la valeur peut en contenir", () => {
  const r = parseConfig("intranet : https://exemple.fr:8080/portail");
  assertEqual(r.rules[0].replacement, "https://exemple.fr:8080/portail");
});

test("config : doublon insensible à la casse, la dernière ligne gagne", () => {
  const r = parseConfig("Google : premier\ngoogle : second");
  assertEqual(r.rules.length, 1);
  assertEqual(r.rules[0].replacement, "second");
  assertEqual(r.duplicateKeywordCount, 1);
});

test("config : BOM résiduel toléré, fins de ligne Windows gérées", () => {
  const r = parseConfig("﻿google : x\r\nmyApp : y\r\n");
  assertEqual(r.rules.length, 2);
  assertEqual(r.rules[0].keyword, "google");
});

// ----- Passe dictionnaire (S3.1) ---------------------------------------------

test("dico : toutes les occurrences, casse ignorée", () => {
  const r = clean("Google GOOGLE google", [{ keyword: "google", replacement: "mon-entreprise" }]);
  assertEqual(r.cleanText, "mon-entreprise mon-entreprise mon-entreprise");
  assertEqual(r.stats.config, 3);
  assertEqual(r.spans.length, 3);
});

test("dico : mot-clé long prioritaire", () => {
  assertEqual(
    cleanText("myApp utilise app", [
      { keyword: "app", replacement: "X" },
      { keyword: "myApp", replacement: "Y" },
    ]),
    "Y utilise X");
});

test("dico : pas de remplacement en cascade", () => {
  assertEqual(
    cleanText("alpha et beta", [
      { keyword: "alpha", replacement: "beta" },
      { keyword: "beta", replacement: "gamma" },
    ]),
    "beta et gamma");
});

test("dico : remplacement contenant son mot-clé ne boucle pas", () => {
  const r = clean("user", [{ keyword: "user", replacement: "nom-user" }]);
  assertEqual(r.cleanText, "nom-user");
  assertEqual(r.spans.length, 1);
});

test("dico : spans exacts dans le texte final, y compris emoji/accents", () => {
  const r = clean("héllo 🚀 google 🚀 été", [{ keyword: "google", replacement: "X" }]);
  assertEqual(r.spans.length, 1);
  assertEqual(r.cleanText.slice(r.spans[0].start, r.spans[0].start + r.spans[0].length), "X");
});

test("dico : fins de ligne CRLF préservées", () => {
  assertEqual(
    cleanText("l1 google\r\nl2 google\r\n", [{ keyword: "google", replacement: "X" }]),
    "l1 X\r\nl2 X\r\n");
});

// ----- Détecteurs (S4.2 à S4.5) ----------------------------------------------

test("D-01 : nom d'utilisateur dans un chemin Windows", () => {
  assertEqual(cleanText("C:\\Users\\jdupont\\Documents"), "C:\\Users\\XX_USER_XX\\Documents");
  assertEqual(cleanText("c:\\users\\jdupont\\src"), "c:\\users\\XX_USER_XX\\src");
  assertEqual(cleanText("D:\\Users\\jdupont"), "D:\\Users\\XX_USER_XX");
});

test("D-01 : profils génériques intacts", () => {
  assertEqual(cleanText("C:\\Users\\Public\\Desktop"), "C:\\Users\\Public\\Desktop");
});

test("D-01 : nom avec espace suivi d'un backslash", () => {
  assertEqual(cleanText("C:\\Users\\John Doe\\Documents"), "C:\\Users\\XX_USER_XX\\Documents");
});

test("D-01 : C:\\Users\\ sans nom n'avale pas la suite de la phrase", () => {
  const input = "le chemin C:\\Users\\, l'e-mail et le reste de la phrase";
  assertEqual(cleanText(input), input);
});

test("D-01 : nom en fin de phrase, sans la ponctuation", () => {
  assertEqual(cleanText("voir C:\\Users\\jdupont. Ensuite…"), "voir C:\\Users\\XX_USER_XX. Ensuite…");
});

test("D-02 : serveur UNC remplacé, partage conservé", () => {
  assertEqual(cleanText("\\\\SRV-PARIS\\commun\\doc.txt"), "\\\\XX_HOST_XX\\commun\\doc.txt");
});

test("D-12 : écho du nom relevé par D-01, jdupont2 intact", () => {
  assertEqual(
    cleanText("le poste de jdupont : C:\\Users\\jdupont\\AppData. Contacter jdupont."),
    "le poste de XX_USER_XX : C:\\Users\\XX_USER_XX\\AppData. Contacter XX_USER_XX.");
  const r = cleanText("C:\\Users\\jdupont\\x et l'utilisateur jdupont2");
  if (!r.includes("jdupont2")) throw new Error("jdupont2 ne doit pas être touché");
});

test("D-03 : e-mail unique puis e-mails numérotés, jeton stable", () => {
  assertEqual(cleanText("Contact : paul.dupont@societe.fr"), "Contact : XX_EMAIL_XX");
  assertEqual(
    cleanText("a@x.fr puis b@y.fr puis encore a@x.fr"),
    "XX_EMAIL_1_XX puis XX_EMAIL_2_XX puis encore XX_EMAIL_1_XX");
});

test("D-04 : IPv4 remplacée, exclusions et faux positifs intacts", () => {
  assertEqual(cleanText("serveur 192.168.1.42 injoignable"), "serveur XX_IP_XX injoignable");
  assertEqual(cleanText("ping vers 10.0.0.7."), "ping vers XX_IP_XX.");
  for (const intact of ["localhost 127.0.0.1 ok", "doc 192.0.2.1 ok", "pas une ip 999.1.1.1 ok", "version 1.2.3.4.5 ok"]) {
    assertEqual(cleanText(intact), intact);
  }
});

test("D-05 : téléphones français", () => {
  for (const input of ["06 12 34 56 78", "06.12.34.56.78", "+33 6 12 34 56 78"]) {
    assertEqual(cleanText(`appelez le ${input} svp`), "appelez le XX_TEL_XX svp", input);
  }
});

test("D-06 : IBAN valide remplacé, checksum invalide intacte", () => {
  assertEqual(cleanText("IBAN : FR14 2004 1010 0505 0001 3M02 606"), "IBAN : XX_IBAN_XX");
  assertEqual(cleanText("IBAN : FR1420041010050500013M02606"), "IBAN : XX_IBAN_XX");
  const invalid = "IBAN : FR15 2004 1010 0505 0001 3M02 606";
  assertEqual(cleanText(invalid), invalid);
});

test("D-07 : NIR avec clé valide remplacé, clé invalide intacte", () => {
  assertEqual(cleanText("NIR 1 85 05 78 006 084 91 fin"), "NIR XX_NIR_XX fin");
  assertEqual(cleanText("NIR 185057800608491 fin"), "NIR XX_NIR_XX fin");
  const invalid = "NIR 1 85 05 78 006 084 92 fin";
  assertEqual(cleanText(invalid), invalid);
});

test("D-08 : carte bancaire Luhn valide remplacée, invalide intacte", () => {
  assertEqual(cleanText("CB 4539 1488 0343 6467 ok"), "CB XX_CB_XX ok");
  assertEqual(cleanText("CB 4539148803436467 ok"), "CB XX_CB_XX ok");
  const invalid = "CB 4539 1488 0343 6468 ok";
  assertEqual(cleanText(invalid), invalid);
});

test("D-09 : GUID alerté sans modification", () => {
  const input = "id 550e8400-e29b-41d4-a716-446655440000 fin";
  const r = clean(input, []);
  assertEqual(r.cleanText, input);
  assertEqual(r.spans.length, 1);
  assertEqual(r.spans[0].kind, "alert");
  assertEqual(r.stats.alerts, 1);
});

test("D-10 : secrets probables alertés, hash hexadécimal pur intact", () => {
  for (const input of [
    "cle sk-abc123def456ghi789jkl012 fin",
    "token ghp_abcdefghij1234567890KLMNOP fin",
    "aws AKIAIOSFODNN7EXAMPLE fin",
    "jeton aB3dE5gH7jK9mN1pQ4sT fin",
  ]) {
    const r = clean(input, []);
    assertEqual(r.cleanText, input, input);
    assertEqual(r.spans.length, 1, input);
    assertEqual(r.spans[0].kind, "alert", input);
  }
  const hex = clean("commit 3f786850e387550fdab836ed7e6dc881de23001b fin", []);
  assertEqual(hex.spans.length, 0);
});

test("D-11 : URL interne alertée, URL publique connue ignorée", () => {
  const r = clean("voir https://intranet.societe.fr/page et https://github.com/org/repo", []);
  assertEqual(r.spans.length, 1);
  assertEqual(r.spans[0].original, "https://intranet.societe.fr/page");
  assertEqual(r.spans[0].kind, "alert");
});

// ----- Cadre commun (S4.1) et cas limites (S6.1) ------------------------------

test("une zone couverte par la config n'est pas re-détectée", () => {
  const r = clean("mail : paul.dupont@societe.fr",
    [{ keyword: "paul.dupont@societe.fr", replacement: "contact-anonyme" }]);
  assertEqual(r.cleanText, "mail : contact-anonyme");
  assertEqual(r.stats.config, 1);
  assertEqual(r.stats.auto, 0);
});

test("statistiques : config, auto et alertes distingués", () => {
  const r = clean(
    "user google : C:\\Users\\jdupont\\x, mail a@x.fr, id 550e8400-e29b-41d4-a716-446655440000",
    [{ keyword: "google", replacement: "mon-entreprise" }]);
  assertEqual(r.stats, { config: 1, auto: 2, alerts: 1, total: 3 });
});

test("cas limites : texte vide, emoji seuls, fins de ligne mixtes", () => {
  assertEqual(clean("", []).cleanText, "");
  const emoji = "🚀🎉🧹✨🔥".repeat(50);
  assertEqual(cleanText(emoji), emoji);
  assertEqual(
    cleanText("l1 google\r\nl2 google\nl3 google\rl4 google", [{ keyword: "google", replacement: "X" }]),
    "l1 X\r\nl2 X\nl3 X\rl4 X");
});

test("tous les spans correspondent au texte final", () => {
  const r = clean("C:\\Users\\jdupont\\x, a@x.fr, b@y.fr, 192.168.1.42, 06 12 34 56 78", []);
  for (const span of r.spans) {
    const extract = r.cleanText.slice(span.start, span.start + span.length);
    if (span.kind === "replaced") {
      if (!extract.startsWith("XX_") || !extract.endsWith("_XX")) {
        throw new Error(`span remplacé inattendu : ${extract}`);
      }
    } else {
      assertEqual(extract, span.original);
    }
  }
});

// ----- Bilan -----------------------------------------------------------------

if (failures.length > 0) {
  console.error(`\n✗ ${failures.length} échec(s), ${passed} réussite(s)\n`);
  for (const { name, error } of failures) {
    console.error(`ÉCHEC : ${name}\n${error.message}\n`);
  }
  process.exit(1);
}
console.log(`✓ ${passed} tests du portage web réussis`);
