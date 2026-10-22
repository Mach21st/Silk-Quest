# Silk Quest 2.0 · Android

A full offline quest journal with nested steps, a personal reward shop, rosary purchases, square image cropping, focus sessions and pinned quest notifications.

## Install and update

Download **SilkQuest-Android.apk** from [the latest release](https://github.com/Mach21st/Silk-Quest/releases/latest). Android 8.0 or later is required; Android 16 is supported. Install over the previous companion or preview APK to keep your local data. All published APKs use the same signing key.

The journal opens and works without a PC. To synchronize, use **Silk Quest 2.0 on both devices**.

## Pair over home Wi-Fi

1. Put the PC and phone on the same home network.
2. On Windows: **Settings → Phone · Wi-Fi → Show QR**.
3. On Android: **Settings → Connect to your PC → Scan QR**. You can also paste the pairing link.
4. Choose a quest and use **Pin in notification** if you want it in your notification shade. Allow Android notifications when asked.

The PC must be running during synchronization, including when minimized to its tray. The journal remains usable when your PC is off. Edits are saved locally with a durable queue and are sent on reconnection. Repeated requests do not grant or charge rewards twice.

Quests, nested steps, names, descriptions, completion, rewards, shop items and pictures, purchases, balance and the pinned quest are synchronized. If both sides edit the same field, the app asks which edit to keep. Other queued edits remain intact. The offline balance is provisional until the PC accepts the changes.

Focus timers, language and appearance are independent on each device. Background synchronization uses the pinned foreground notification; without notification permission, synchronization runs while the app is open. Android battery restrictions can affect background work.

## Backups

Use **Settings → Your journal → Export a backup** to export quests, items and pending edits. The file contains no pairing credentials. Import a backup through the Android document picker. Export before switching computers. Resetting the phone does not erase the PC journal.

On Windows, back up the `data` folder before updating. Replace the EXE while keeping `data` beside it.

## Build

Use JDK 21, Android SDK Platform 36 and Build Tools 36 on Windows. The ZXing dependency is included with its license.

```powershell
./build.ps1 -AndroidJar C:/Android/platforms/android-36/android.jar -BuildTools C:/Android/build-tools/36.0.0 -SigningKey C:/private/release.jks -OutputApk C:/output/SilkQuest-Android.apk
```

A build without `-SigningKey` creates a development key. Keep the release key private and reuse it for updates; another key cannot update an existing installation. The local script uses the development alias and password shown in its source; adapt signing to your own release credentials.

The build normalizes APK paths before alignment and signing, then validates the final ZIP names. HTML, CSS and JavaScript remain ordinary bundled assets. No server or frontend build service is required.

Model checks are in `tests/run.ps1`; actual Android WebView, scroll and restart checks are in `tests/device/`. These device tests run only against an emulator. Camera permissions, battery policies and notification behavior can differ by phone.
