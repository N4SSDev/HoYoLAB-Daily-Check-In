<div align="center">

# Daily Check-In 🔴

**`Status: Operational`** | **`Version: 1.0.0`**
<br>
*Module d'automatisation autonome du check-in quotidien HoYoLAB avec télémétrie Discord native et empreinte système minimale.*

![GitHub repo size](https://img.shields.io/github/repo-size/N4SSDev/Daily-Check-In?style=for-the-badge&color=000000)
![GitHub last commit](https://img.shields.io/github/last-commit/N4SSDev/Daily-Check-In?style=for-the-badge&color=ff0000)

</div>

---

### > SYSTEM_OVERVIEW

Agent autonome x64 sous Windows orchestrant la réclamation silencieuse des récompenses quotidiennes HoYoLAB (Genshin Impact) au démarrage système via l'API web publique. Embarque un dispatcher de télémétrie Discord riche (embeds d'inventaire, métriques de résine en temps réel, statut des commissions) sans runtime externe, ramenant l'empreinte mémoire à 41 Mo et le binaire à un fichier unique de 163 Ko.

---

### > CORE_STACK

> **`[ T E C H N O L O G I E S ]`**<br>
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET_Framework_4.5+-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![PowerShell](https://img.shields.io/badge/PowerShell-5391FE?style=for-the-badge&logo=powershell&logoColor=white)
![Windows](https://img.shields.io/badge/Windows_API-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![Discord API](https://img.shields.io/badge/Discord_Webhooks-5865F2?style=for-the-badge&logo=discord&logoColor=white)

---

### > SETUP_PROTOCOL

> **`[ 1 . I N S T A L L A T I O N ]`**
Cloner le dépôt source :
```bash
git clone https://github.com/N4SSDev/Daily-Check-In.git
```
Compiler l'exécutable natif sans installer aucun SDK tiers (utilise le compilateur `csc.exe` intégré nativement à Windows) :
```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test
```
Le binaire optimisé est généré dans `out\DailyCheckIn.exe`.

> **`[ 2 . E X E C U T I O N ]`**
Pour ouvrir l'interface de configuration graphique :
```bash
.\out\DailyCheckIn.exe
```
Pour installer le daemon en tâche de fond au démarrage de Windows :
```bash
.\out\DailyCheckIn.exe --install
```
> *Note opérationnelle : Pour une utilisation directe sans compilation, l'exécutable autonome précompilé est disponible au téléchargement dans l'onglet **Releases** du dépôt GitHub.*

---

### > COMMS_LINK

[![Discord](https://img.shields.io/badge/-Discord-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.com)
[![GitHub](https://img.shields.io/badge/-N4SSDev-000000?style=for-the-badge&logo=github&logoColor=ff0000)](https://github.com/N4SSDev)
