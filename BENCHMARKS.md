# Daily Check-In

Check-in quotidien HoYoLAB (Genshin Impact) au démarrage de Windows : l'app se lance sans fenêtre,
réclame la récompense, poste un embed Discord, affiche une notification, puis se ferme.

**Un seul fichier de 163 Ko. Aucune dépendance.** Tout ce qu'elle utilise est déjà dans Windows.

L'app ne touche à **aucun fichier du jeu** : elle appelle uniquement l'API web publique de
`act.hoyolab.com`, les mêmes requêtes que le bouton *Check In* du site.

---

## Ce que la refonte a changé

Mesures prises sur la même machine, avec la même méthode, avant et après. Aucun chiffre estimé.

| | Electron | C# natif | |
|---|---|---|---|
| Dossier déployé | 367 Mo (73 fichiers) | **163 Ko** (1 fichier) | ÷ 2 300 |
| Installeur | 106 Mo | **aucun** (l'exe s'installe) | — |
| Dépendances | 223 paquets npm (448 Mo) | **0** | — |
| Processus | 3 | **1** | |
| Threads | 90 | **22** | |
| Pic RAM (working set) | 197 Mo | **41,3 Mo** | ÷ 4,8 |
| Pic RAM privée | 141 Mo | **27,4 Mo** | ÷ 5,1 |
| CPU par exécution | 422 ms | **156 ms** | − 63 % |
| Durée du travail utile | 1,8 – 3,7 s | **1,2 – 1,9 s** | |
| Journal par exécution | 314 octets | **~85 octets** | ÷ 3,7 |
| État sur disque | croissant (20 entrées) | **183 octets, borné** | |
| Compilation | ~10 min | **170 ms** | |
| Tests | 50 | **106** | |

**Au repos, les deux consomment exactement zéro** : aucun service, aucune tâche résidente, aucun
processus entre deux exécutions. Il n'y avait rien à gagner de ce côté, et rien n'a été inventé.

Une nuance d'honnêteté sur la durée : le processus **vit plus longtemps** qu'avant (≈ 9,5 s contre
1,9 s) parce qu'il attend désormais que la notification Windows soit prise en compte — ce que la
version Electron ne faisait pas, raison pour laquelle aucune notification n'apparaissait jamais.
Le travail réel, lui, est plus rapide, et le CPU consommé est divisé par près de trois.

### D'où venaient les 367 Mo

Le code utile de l'ancienne version pesait 257 Ko ; le runtime autour en pesait 367. Un rapport de
1 à 1 400 pour une application qui fait quatre requêtes HTTPS et affiche une bulle. Windows fournit
déjà tout le nécessaire — `csc.exe`, HTTP, TLS, JSON, WinForms, Planificateur de tâches — et
`csc.exe` est présent sur toute installation depuis le .NET Framework 4.

---

## Compiler

```bash
powershell -ExecutionPolicy Bypass -File build.ps1
```

Produit `out\DailyCheckIn.exe` en environ 170 ms. Avec `-Test`, la suite de 106 tests est compilée
et exécutée ; toute erreur interrompt la compilation (`/warnaserror+`).

Aucun SDK à installer : le compilateur utilisé est celui livré avec Windows
(`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, C# 5).

## Installer

Double-clic sur `DailyCheckIn.exe`, ou :

```bash
DailyCheckIn.exe --install
```

L'app se copie dans `%LOCALAPPDATA%\Programs\DailyCheckIn`, pose un raccourci au menu Démarrer et
s'inscrit dans « Applications et fonctionnalités ». La désinstallation passe par Windows, ou par
`DailyCheckIn.exe --uninstall`.

Au premier lancement, la configuration de la version Electron
(`%APPDATA%\genshin-daily-checkin`) est **reprise automatiquement** : rien à ressaisir.

## Modes

| Argument | Effet |
|---|---|
| *(aucun)* | fenêtre de configuration |
| `--background` | check-in silencieux, puis sortie — utilisé par la tâche planifiée |
| `--install` | installe et lance |
| `--uninstall` | retire tout (`--silent` pour ne rien demander) |

---

## Configuration

`F12` sur [hoyolab.com](https://www.hoyolab.com) → **Application** → Cookies → sélectionne tout le
tableau et colle-le dans le champ. Le tri est automatique : sur une trentaine de cookies, seuls
`ltoken_v2` et `ltuid_v2` sont conservés, **le reste n'est jamais écrit sur disque**.

Formats avalés indifféremment : tableau DevTools (tabulations), en-tête `Cookie: …`, sortie de
`document.cookie`, lignes `clé=valeur`, ou une ligne déjà propre — le tri est idempotent.

| Réglage | Défaut | Effet |
|---|---|---|
| Ping `@everyone` | Si échec | Jamais / Si échec / À chaque check-in |
| Envoyer l'embed même si déjà fait | activé | un embed à chaque démarrage, données fraîches |
| Résine, commissions & boss | activé | trois champs alignés dans l'embed |
| Tous mes serveurs | désactivé | une ligne par serveur Genshin lié |
| Notifications Windows | les trois | réclamée · déjà récupérée · échec |

Fichiers dans `%APPDATA%\DailyCheckIn\` : `config.json`, `state.json`, `checkin.log`.

---

## Décisions d'architecture

**Requêtes parallélisées.** `/info`, `/home` et la liste des comptes sont indépendantes : lancées
ensemble, elles économisent deux allers-retours (~400 ms mesurés par requête). Les notes de
plusieurs serveurs le sont aussi.

**Réglages réseau.** `Expect100Continue` désactivé (un aller-retour perdu à chaque POST),
Nagle désactivé (jusqu'à 200 ms sur des requêtes aussi courtes), gzip activé, proxy non détecté.

**Tâche planifiée via l'API COM**, pas `schtasks.exe` : ni processus enfant, ni fichier XML
temporaire. Déclencheur unique à l'ouverture de session — aucune heure fixe.

**La clé de registre `Run` n'est pas utilisée.** Windows la déclenche avec un retard variable et
n'en journalise rien : impossible de distinguer « pas encore lancé » de « jamais lancé ». Une tâche
planifiée est visible, journalisée, et se crée sans élévation.

**Attente événementielle.** La notification est attendue par événements, pas par scrutation : la
boucle de messages dort tant que rien n'arrive. C'est ce qui ramène le CPU à 156 ms sur 9,7 s.

**WinForms chargé à la demande.** Mesuré : 25 Mo sans, 31 Mo avec. Le chemin arrière-plan ne le paie
que s'il notifie réellement.

**Verrou par mutex nommé**, sans fichier sur disque.

**Écritures conditionnelles.** `state.json` n'est réécrit que si son contenu a changé, et il est de
taille fixe : une entrée par compte, pas d'historique qui croît.

**Un enregistrement de journal par exécution.** Le détail n'apparaît qu'en cas d'erreur. Rotation à
64 Ko, un seul fichier de secours.

### Les deux pièges des notifications Windows

`ShowBalloonTip` ne fait que **confier** la bulle au shell, de façon asynchrone. Quitter aussitôt
tue le processus avant l'affichage : c'est pourquoi aucune notification n'apparaissait dans la
version Electron. L'app attend donc `BalloonTipShown` — un événement, pas un délai deviné — puis
l'acquittement, plafonné à 2 minutes pour ne jamais rester en mémoire.

Second piège : demander la bulle juste après avoir rendu l'icône visible la fait disparaître en
silence, Windows ne l'ayant pas encore enregistrée. On lui laisse 600 ms.

### Le cas « déjà fait aujourd'hui »

Une date de dernier succès (UTC+8) évite de reposter le même embed. Elle est **subordonnée** à la
case « envoyer même si déjà fait » : cochée, chaque démarrage refait le tour complet et poste des
données à jour ; décochée, l'app sort en quelques millisecondes sans aucune requête, et la
notification « Déjà récupérée » évite que le silence ne ressemble à une panne.

### Réparation du démarrage

La tâche enregistre un chemin absolu. Si l'exécutable disparaît, Windows tente de lancer un fichier
absent : rien ne se passe, rien n'est journalisé. À chaque ouverture de la fenêtre, l'app vérifie
que la cible **existe encore** et la réécrit sinon.

Elle vérifie l'existence, pas l'égalité avec son propre chemin : sinon deux copies de l'app se
voleraient le démarrage à chaque ouverture.

---

## Structure

```
src/
  Program.cs       point d'entrée, modes, verrou d'instance
  Runner.cs        orchestration, cache journalier, résumé
  HoyoLab.cs       API de check-in, retcodes, requêtes parallèles
  Notes.cs         résine, commissions, boss (signature ds)
  Discord.cs       construction et envoi de l'embed
  Notifier.cs      notification Windows, attente événementielle
  AutoStart.cs     tâche planifiée via COM
  Installer.cs     installation, désinstallation, raccourci
  Config.cs        réglages, validation, reprise de l'ancienne version
  State.cs         état borné, écriture conditionnelle
  CookieParser.cs  tri d'un collage brut
  Http.cs          couche HTTP, reprises, point d'injection de test
  Json.cs          lecture tolérante, écriture lisible
  Log.cs           journal avare, rotation
  Paths.cs         emplacements
  ConfigForm.cs    fenêtre de configuration
tests/Tests.cs     106 tests, sans framework
tools/Shot.cs      rend la fenêtre en PNG (développement)
build.ps1
```

## Tests

```bash
powershell -ExecutionPolicy Bypass -File build.ps1 -Test
```

106 tests sans réseau : tri des cookies (tableau DevTools, en-tête, idempotence, multi-comptes,
collage inutilisable), JSON (tolérance aux réponses invalides, échappement, aller-retour),
configuration et validation, état et écriture atomique, embed Discord (mise en page, champs
alignés courts, modes de ping, barre de progression, pied de page), signature `ds`, dates UTC+8,
et le check-in complet par injection : succès, déjà fait, passage d'un mois à l'autre, compteur
incohérent, cookie mort, captcha, `first_bind`, reprise après deux erreurs 500.
