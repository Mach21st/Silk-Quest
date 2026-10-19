# Silk Quest — Windows

A local quest journal with nested tasks, a focus timer, rosary rewards, a personal reward shop and a pinned window above other apps.

## Run

For the standalone Windows x64 release, extract the ZIP into a writable folder and open **SilkQuest.exe**. Windows 10/11 x64 is supported. No separate .NET installation is required for this release. The executable is not code-signed.

The first launch uses English and includes editable example quests and a **Pepsi Bottle** shop item priced at 15 rosaries. Choose **Settings → Language → Русский** for Russian. Your own quest and product names are kept as written when switching languages.

Existing journals keep their selected language, tasks, purchases and settings when updating. Close the app using **Settings → Quit application** or the tray menu before replacing the executable. Keep the `data` folder.

## Quests and focus

- Add tasks with **Add quest** or Ctrl+N. Use the ··· menu to edit tasks, add steps or pin a quest.
- Completing a parent completes its steps. Reopening a step reopens its parents. Rewards can only be earned once per task.
- Use **Undo** or Ctrl+Z outside a text field within ten seconds to undo the latest completion or deletion.
- **Settings → Focus** offers 1–180 minutes, exact entry and 15/25/45/60-minute presets. Press Enter or leave the input to apply an exact value. Changing the default keeps running and paused sessions intact; reset or start a new session to use the new duration.
- The timer is tied to one task at a time. After restarting the app it is paused.

## Pinned quest

Select a quest and choose **Pin above all windows**. Drag the small top bar to move the window; resize it from the edges. Settings include opacity, text size, compact mode and corner positions. The window remembers its position.

The main window's close button hides it in the Windows tray. Double-click the tray icon to reopen it. Use the tray menu or Settings to quit completely.

## Shop and rewards

Add a symbolic reward with the + card. Enter a name, description and price, choose an image, and adjust its square crop by dragging and zooming. Images are stored with the journal; the original file is no longer needed.

Click a product to inspect or buy it. Purchases deduct rosaries and are recorded with their price at purchase time. Insufficient funds disable buying. Product previews fade out after the pointer stays outside for 1.1 seconds, or after a click elsewhere.

Base rewards can be changed in Settings. Individual quests can override their reward. Previously claimed rewards are not recalculated.

## Data and backups

`data/journal.json` is stored next to the executable. Keep this folder when moving or updating the app. Previous saves and up to seven daily snapshots are available under **Settings → Your journal → Restore**. You can also export a backup. Restore brings back quests, shop items, purchases and balance together while keeping current phone credentials.

The app uses no account or cloud service. Optional phone sync sends the pinned quest over your home network to the paired Android companion. On Windows, enable **Settings → Phone · Wi-Fi**, show the QR code, then scan it in the Android app. Choose a quest using **··· → Pin to phone**. See `WIFI_PROTOCOL.md` and the Android README for details. The Android companion currently uses Russian UI.

## Build

Build on Windows with a .NET SDK supporting .NET 8/WPF:

```powershell
dotnet publish SilkJournal.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o publish
```

Rename `publish/SilkJournal.exe` to `SilkQuest.exe` if desired. The starter image and English translations are embedded. A framework-dependent build is also available with `dotnet publish SilkJournal.csproj -c Release -o app`; that build requires .NET Desktop Runtime 8.

Verification flags: `--self-test`, `--verify-ui`, `--verify-phone`. Tests use separate data directories; the phone check uses localhost port 38929. These flags produce test artifacts beside the executable, so run them on a test copy rather than inside a release ZIP.

QRCoder and bundled .NET runtime license notices accompany the distribution. Android includes ZXing notices.
