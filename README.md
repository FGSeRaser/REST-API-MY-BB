## 🚀 MyBB Thread Creation API
Dieses Projekt bietet eine extrem schnelle und sichere Möglichkeit, Threads in einem MyBB-Forum über eine REST-Schnittstelle zu erstellen.
Ideal für Automatisierungen, mobile Apps oder Cross-Posting-Tools.


## ✨ Hauptfunktionen
```text
High Performance – Direkte Datenbankverbindung (umgeht den schweren MyBB-Core)

Secure – Authentifizierung via API-Key (gebunden an MyBB-User)

Easy Integration – Plugin zur Key-Verwaltung im UserCP

Logging – Debug- & Error-Logging im api/logs/ Verzeichnis
```
## 📂 Installation & Verzeichnisstruktur
```text
.
├── api/
│   ├── threads_create.php    # Der API-Endpunkt
│   └── logs/                 # Log-Verzeichnis (CHMOD 777)
└── inc/plugins/
    └── api.php               # Das Key-Management-Plugin
🚀 Installation

    Plugin hochladen: Kopiere die api.php in deinen inc/plugins/ Ordner.

    Aktivieren: Gehe im MyBB Admin-Panel (ACP) auf "Plugins" und aktiviere das MyBB API Plugin.

    Key generieren: - Gehe in dein UserCP (Benutzer-Kontrollzentrum).

    Klicke auf den neuen Menüpunkt API-Verwaltung.

    API-Key generieren (Standard: Admin-Gruppe ID 4)

    API-Ordner bereitstellen: Lade den api/-Ordner hoch und erstelle darin den Unterordner logs/. Stelle sicher, dass der Webserver im logs/-Ordner schreiben darf.
```



## 🛠 API Nutzung
```text
POST Request an:

https://yourdomain.com/api/threads_create.php
Beispiel JSON Payload
{
  "name": "DeinBenutzername",
  "api_key": "DEIN-API-KEY",
  "fid": 2,
  "subject": "Test Thread via API",
  "message": "Dies ist der Inhalt des Threads.",
  "allow_mycode": true
}
```

## 🖥 Terminal Test
```text
curl -X POST https://deine-domain.de/api/threads_create.php \
     -H "Content-Type: application/json" \
     -d '{
          "name": "DeinAdminName",
          "api_key": "DEIN-KEY-AUS-DEM-USERCP",
          "fid": 2,
          "subject": "GitHub Test",
          "message": "Dieser Thread wurde per API-Test erstellt."
         }'
```

## 🔒 Sicherheit
```text
Schutz vor SQL-Injections durch real_escape_string()

API-Key-Erstellung auf Admin-Gruppe (ID 4) beschränkt

Empfehlung: Nur über HTTPS verwenden
```

## 📜 Lizenz
```text
MIT License
```
