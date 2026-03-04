# REST-API-MY-BB
🚀 A high-performance REST-API for MyBB forums to manage threads, posts, and user activities. Includes a secure API-Key management plugin for the UserCP. Optimized for mobile integration and administrative automation.
# MyBB REST-API Suite 🚀

Diese API ermöglicht es, ein MyBB-Forum (1.8.x) programmatisch zu steuern. Der Fokus liegt auf der Erstellung von Threads, dem Auslesen von Aktivitäten und der sicheren Authentifizierung via API-Key direkt über das MyBB-System.

## ✨ Features

* **Thread Creation API:** Schnelles Erstellen von Threads unter Umgehung des schweren MyBB-Cores für maximale Performance.
* **API-Key Management:** Integriertes MyBB-Plugin, das Admins erlaubt, Keys direkt im UserCP zu generieren.
* **Mobile Ready:** Optimierte Endpunkte für die Nutzung in Android/iOS Apps oder externen Dashboards.
* **Security First:** Schutz gegen SQL-Injection durch `real_escape_string` und strikte Gruppenprüfung (nur Admins).
* **Logging:** Detaillierte Debug- und Error-Logs für alle API-Vorgänge.

## 📁 Ordnerstruktur

Damit die API korrekt funktioniert, sollte sie wie folgt im MyBB-Hauptverzeichnis abgelegt werden:

```text
/ (MyBB Root)
├── api/
│   ├── threads_create.php    <-- Haupt-Endpunkt
│   └── logs/                 <-- (Schreibrechte 777 erforderlich)
└── inc/plugins/
    └── api.php               <-- Das MyBB-Plugin
