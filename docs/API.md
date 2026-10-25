# FG Forum API – Routen

Endpunkt: `https://dein-forum.de/api/?route=<route>`

- **GET**-Parameter hängen an der URL.
- **POST**-Daten gehen als JSON-Body (`Content-Type: application/json`) oder als Formular.
- Uploads gehen als `multipart/form-data`.

Anmeldung per Header `X-FG-Token`, `X-FG-Key` oder `Authorization: Bearer`. Ohne Anmeldung gehen nur `info` und `auth/login`.

Zeitangaben sind Unix-Zeitstempel (Sekunden).

## Allgemein

| Route | Methode | Parameter | Antwort |
|---|---|---|---|
| `info` | GET | – | Forenname, URL, API- und MyBB-Version, Login-Arten, Statistik |
| `auth/login` | POST | `username`, `password`, `client` (Gerätename, optional) | `token`, `expires`, `user` (wie `me`) |
| `auth/logout` | POST | – | beendet die aktuelle Anmeldung (Token) |
| `me` | GET | – | eigener Benutzer, ungelesene PNs, Rechte, erlaubte Dateitypen |
| `online` | GET | – | Benutzer online, Gäste, Forenstatistik |
| `user` | GET | `uid` oder `username` | Profil (Beiträge, Themen, Registrierung, letzter Besuch, Signatur) |

## Lesen

| Route | Methode | Parameter | Antwort |
|---|---|---|---|
| `forums` | GET | – | alle sichtbaren Foren als Liste mit `pid`, `depth`, Zählern, letztem Beitrag und Rechten (`can`) |
| `threads` | GET | `fid`, `page`, `perpage` (max. 100) | Themen eines Forums, angepinnte zuerst |
| `thread` | GET | `tid`, `page` (Zahl oder `last`), `perpage` (max. 50), `pid` (springt zur Seite mit diesem Beitrag) | Thema + Beiträge mit `message` (MyCode), `html`, Anhängen und Rechten (`can.edit`, `can.delete`) |
| `post/get` | GET | `pid` | ein Beitrag (z. B. zum Bearbeiten) |
| `latest` | GET | `limit` (max. einstellbar), `type` = `posts` oder `threads` | neueste Beiträge bzw. Themen |
| `search` | GET | `q`, `titles_only`, `fid`, `author`, `page` | Treffer mit Auszug |
| `preview` | POST | `message` und `fid` oder `tid` oder `pm=1` | `html` so, wie das Forum es anzeigen würde |

Im `html` wird `[attachment=X]` als `<span class="fg-attachment" data-aid="X"></span>` geliefert. Den Platzhalter ersetzt der Client durch das Bild.

## Schreiben

| Route | Methode | Parameter |
|---|---|---|
| `thread/create` | POST | `fid`, `subject`, `message`, `posthash` (für Anhänge), `prefix`, `signature`, `close`/`stick` (nur Moderatoren) |
| `post/reply` | POST | `tid`, `message`, `subject` (optional), `posthash`, `replyto`, `signature` |
| `post/edit` | POST | `pid`, `message`, `subject`, `editreason` |
| `post/delete` | POST | `pid` (der erste Beitrag löscht das ganze Thema; Papierkorb, wenn „Soft Delete“ aktiv ist) |
| `thread/moderate` | POST | `tid`, `action` = `close`, `open`, `stick`, `unstick` |

Fehler beim Prüfen kommen mit HTTP 422 und den MyBB-Meldungen in `error.details`, z. B. „Nachricht zu kurz“.

## Anhänge

| Route | Methode | Parameter |
|---|---|---|
| `attachment/upload` | POST (multipart) | `file` plus **entweder** `posthash` + `fid`/`tid` (neuer Beitrag) **oder** `pid` (bestehender Beitrag) |
| `attachment/delete` | POST | `aid` |
| `attachment` | GET | `aid`, `thumb=1` für das Vorschaubild. Antwort ist die Datei selbst. |

Es gelten die Regeln des Forums: Dateitypen, Größen, Kontingent, maximale Anzahl pro Beitrag und Moderation von Anhängen.

## Private Nachrichten

| Route | Methode | Parameter |
|---|---|---|
| `pms` | GET | `folder` (1 Posteingang, 2 Gesendet, 3 Entwürfe, 4 Papierkorb, eigene Ordner ab 5), `page` |
| `pm` | GET | `pmid` (markiert als gelesen) |
| `pm/send` | POST | `to` (Name oder Liste, mit Komma getrennt), `subject`, `message`, `replyto`, `savecopy` |
| `pm/delete` | POST | `pmid` (erst Papierkorb, dort endgültig) |

## Eigene Zugänge

| Route | Methode | Parameter |
|---|---|---|
| `keys` | GET | – (alle Keys und Anmeldungen des Benutzers, `current` = dieser Zugang) |
| `keys/create` | POST | `label`. Geht nur nach einem Login mit Passwort. Der Key wird **einmal** zurückgegeben. |
| `keys/revoke` | POST | `id` oder `all=1` (alle außer dem aktuellen) |

## Fehlercodes (Auswahl)

| Code | HTTP | Bedeutung |
|---|---|---|
| `auth_required`, `auth_invalid`, `auth_expired` | 401 | nicht oder nicht mehr angemeldet |
| `login_failed` | 401 | Benutzername oder Passwort falsch |
| `too_many_attempts`, `account_locked` | 429 | zu viele Fehlversuche |
| `no_permission`, `banned`, `no_api_access` | 403 | keine Berechtigung |
| `forum_not_found`, `thread_not_found`, `post_not_found` | 404 | gibt es nicht oder ist nicht sichtbar |
| `validation_failed` | 422 | MyBB hat den Inhalt abgelehnt (siehe `details`) |
| `duplicate` | 409 | gleicher Beitrag wurde gerade schon gesendet |
| `board_closed`, `api_disabled`, `plugin_inactive` | 503 | Forum geschlossen oder API aus |
