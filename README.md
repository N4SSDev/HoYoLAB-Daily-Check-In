<div align="center">

# Daily Check-In 🔴

**`Status: Operational`** | **`Version: 1.0.0`**
<br>
*Automated HoYoLAB daily check-in with Discord webhook sender for notifications. Zero dependencies, 164 KB.*

![GitHub last commit](https://img.shields.io/github/last-commit/N4SSDev/HoYoLAB-Daily-Check-In?style=for-the-badge&color=ff0000)

</div>

---

### > ABOUT

Automates your daily HoYoLAB check-in on Windows boot without background overhead.
Sends detailed Discord webhook embeds tracking real-time resin timers, daily commissions, weekly bosses status, and claimed rewards.
Packed into a single 164 KB standalone executable with zero runtime dependencies.

---

### > PREVIEW

<div align="center">
  <table>
    <tr>
      <td width="48%" align="center" valign="top">
        <sub><b>GUI Configuration</b></sub><br><br>
        <img src="assets/app.png" alt="GUI Configuration" />
      </td>
      <td width="52%" align="center" valign="top">
        <sub><b>Discord Notification Embed</b></sub><br><br>
        <img src="assets/embed.png" alt="Discord Webhook Embed" />
      </td>
    </tr>
  </table>
</div>

---

### > BUILT WITH

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET_Framework_4.5+-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![PowerShell](https://img.shields.io/badge/PowerShell-5391FE?style=for-the-badge&logo=powershell&logoColor=white)
![Windows](https://img.shields.io/badge/Windows_API-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![Discord API](https://img.shields.io/badge/Discord_Webhooks-5865F2?style=for-the-badge&logo=discord&logoColor=white)

---

### > GETTING STARTED

> **`[ 1 . I N S T A L L A T I O N ]`**
```bash
git clone https://github.com/N4SSDev/HoYoLAB-Daily-Check-In.git
```

Build using Windows' native `csc.exe` (no SDK required) :

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```
Output: `out\DailyCheckIn.exe`.

> **`[ 2 . E X E C U T I O N ]`**
```bash
.\out\DailyCheckIn.exe
```

> The pre-compiled standalone executable is available in the **[Releases](../../releases)**.

---

### > LINKS

[![Discord](https://img.shields.io/badge/-Discord-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.gg/B5jzC9kdv4)
[![GitHub](https://img.shields.io/badge/-N4SSDev-000000?style=for-the-badge&logo=github&logoColor=ff0000)](https://github.com/N4SSDev)
