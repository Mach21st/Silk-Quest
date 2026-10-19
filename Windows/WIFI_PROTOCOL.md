# Silk Quest local protocol 1

Transport: HTTPS over IPv4, desktop TCP port 38921 by default. Windows accepts only loopback, RFC1918 and link-local IPv4 clients. No URLACL, admin privileges, cloud endpoints or automatic firewall changes. The listener is explicitly enabled in desktop settings and may stay active while the journal is in its tray.

## Pairing

The QR URI is `silkquest://pair?host=PRIVATE_IPV4&port=38921&fp=SHA256_CERT_HEX&secret=RANDOM_128_BIT_HEX`. The Android companion accepts only local IPv4 and validates the URI fields. It uses the exact SHA-256 certificate fingerprint as its TLS trust anchor; redirects and cleartext HTTP are disabled. The private key stays in desktop `app/data/phone-cert.pfx`.

`POST /api/pair` accepts JSON `{ "secret": "…", "device": "…" }`. The secret expires in 5 minutes, is single use and permits at most five attempts. It exists only in process memory. A successful response is `{ "protocol": 1, "token": "RANDOM_256_BIT_HEX" }`. The new token replaces the previous device's token. Pairing changes are saved through the desktop UI dispatcher. Native requests must not include an `Origin` header.

## Authenticated endpoints

Use `Authorization: Bearer TOKEN`. A missing, incorrect or revoked token returns 401. Android clears its paired credentials and pending action on 401.

`GET /api/state?after=REVISION` returns the current pinned quest snapshot. If its revision is unchanged, the request waits up to 25 seconds; otherwise it returns immediately. The companion reconnects with bounded backoff on connection failures. There is no global journal download endpoint.

The snapshot contains `protocol`, `revision`, `generation`, `questVersion`, `balance`, and `quest` (nullable). Quest fields: `id`, `title`, `notes`, `done`, `reward`, `completed`, `total`, `steps`. `steps` lists leaves of a grouped quest. `reward` is the prospective reward of completing the quest, including newly completed ancestors.

`POST /api/complete` accepts `{ "id": "…", "generation": "…", "questVersion": "…" }`. Desktop pending editor text is flushed before validation. The quest must match the currently pinned ID and pin generation. If it is unfinished, its serialized quest hash must also match. A conflict returns 409 and the current snapshot; Android discards the obsolete action and displays current state. A completion of the same already-completed pin returns 200 without a second reward. All actual completion and reward logic goes through the desktop controller and existing persistent reward ledger.

Changing the phone pin creates a new generation even when re-pinning the same quest. Deleting a pinned quest clears the phone pin. Phone pinning does not alter the desktop overlay pin. Unpinning sends `quest: null`; the Android connection notification remains available until explicitly hidden or disconnected.

## Android persistence and lifecycle

App-private preferences contain endpoint, pinned certificate fingerprint, auth token, last snapshot and one pending completion. Backup is disabled in the manifest. Completion is queued before transmission and cleared only after 200/409; network failures leave it queued. The desktop is authoritative for rosaries.

The user starts a `connectedDevice` foreground service from the Activity or notification action. It has a low-importance notification channel, no wake lock, and uses the Wi-Fi/Ethernet network explicitly. Notification posting requires Android's normal permission where applicable. A manually hidden notification pauses the connection; it is not forcibly re-posted. Re-open the companion after phone reboot. Android/OEM background restrictions may delay updates.

## Verification

Run `SilkJournal.exe --verify-phone`. It uses an isolated demo journal and loopback port 38929. Tests cover real TLS, certificate fingerprint, rejection of unauthenticated and incorrect pair requests, single-use code, pin snapshots, shared completion/rewards, duplicate delivery, stale versions, replacement pin generation, long polling, persistence and revocation. QR encoding was decoded with the same ZXing 3.5.3 core used in the APK. Physical Android runtime behavior still needs device validation.

## Durable acknowledgement and polish

A phone completion is acknowledged only after a successful write-through atomic journal save. Pairing is subject to the same rule. On storage failure the server returns 503, restores the pre-action task/reward state, and does not publish unsaved changes. Android keeps its command queued on 503. Completion acknowledgements update the cached state and clear the matching command together. Tests lock the actual temporary save file to reproduce failures, including rollback after mutation.

Snapshot revisions now change only when the phone-visible payload changes, rather than for every local UI save. A completion stamp prevents stale offline actions from re-closing a quest that was completed and reopened on PC. Desktop undo is available for 10 seconds and rolls back only affected completion marks and newly granted reward claims, preserving subsequent text edits. Snapshot restore retains the current device credentials and invalidates the phone pin generation.