using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SilkJournal;

public partial class App : Application
{
 public App()
 {
  DispatcherUnhandledException += (_,e) => { try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"error.log"),e.Exception.ToString()); } catch { } MessageBox.Show(e.Exception.Message,L.T("Ошибка журнала")); e.Handled=true; ExitApp(); };
  AppDomain.CurrentDomain.UnhandledException += (_,e) => { try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"error.log"),e.ExceptionObject.ToString()); } catch { } };
 }
 Mutex? mutex;
 public JournalStore Store { get; private set; } = null!;
 public JournalState State { get; private set; } = null!;
 public MainWindow Journal { get; private set; } = null!;
 public OverlayWindow? Overlay { get; private set; }
 public PhoneBridge Phone {get;private set;}=null!;
 public bool Running { get; private set; }
 public bool Exiting { get; private set; }
 public event Action? Changed;
 public event Action? Tick;
 public event Action? AppearanceChanged;
 public void SetWeathering(bool value){State.Weathering=value;Commit(false);AppearanceChanged?.Invoke();}
 DispatcherTimer timer = new();
 DateTime focusDeadline;
 System.Windows.Forms.NotifyIcon? tray;
 public static App CurrentApp => (App)Current;
 public bool SaveHealthy { get; private set; } = true;
 protected override void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  if(e.Args.Contains("--make-icon")){BrandAssets.WriteIcon(Path.Combine(Directory.GetCurrentDirectory(),"Assets"));Shutdown();return;}
  if(e.Args.Contains("--release-data")){var demo=JournalState.Starter();demo.InitializeRewards();File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"journal-demo.json"),System.Text.Json.JsonSerializer.Serialize(demo,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));Shutdown();return;}
  if (e.Args.Contains("--self-test")) { try { SelfTest.Run(); Shutdown(0); } catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test-failed.txt"),ex.ToString()); Shutdown(1); } return; }
  bool isolated=e.Args.Contains("--verify-ui")||e.Args.Contains("--verify-phone")||e.Args.Contains("--preview-controls")||e.Args.Contains("--preview-design");
  mutex = new Mutex(true, "Local\\SilkJournal-Muz"+(isolated?"-verification":""), out bool first);
  if (!first) { MessageBox.Show(L.T("Журнал уже запущен. Открой его значком в области уведомлений."),L.T("Журнал странника")); Shutdown(); return; }
  try
  {
   // Keep user data next to this portable application; nothing is sent to a server.
   Store = new JournalStore(Path.Combine(AppContext.BaseDirectory, isolated ? "ui-verification-data" : "data")); State = isolated ? JournalState.Demo() : Store.Load();
   L.Language=State.Language=="en"?"en":"ru";State.InitializeRewards();
   Phone=new PhoneBridge(this);if(State.PhoneSyncEnabled)Phone.Start();
   Journal = new MainWindow(this); MainWindow = Journal; Journal.Show();
   if(e.Args.Contains("--preview-controls")){Journal.Title="Silk Quest · Детали интерфейса";Journal.ShowSettings();}
   if(e.Args.Contains("--preview-design"))Journal.Title="Silk Quest · Проверка дизайна";
   if(isolated&&e.Args.Contains("--small")){Journal.Width=1100;Journal.Height=740;}
   if(isolated&&e.Args.Contains("--compact")){Journal.MinWidth=800;Journal.MinHeight=480;Journal.Width=960;Journal.Height=620;}
   if(isolated&&e.Args.Contains("--long-title")){State.Quests[0].Title="Запустить портфолио и собрать лучшие работы для нового путешествия";State.Find(State.SelectedId)!.Title="Собрать главную страницу портфолио, подобрать лучшие проекты и подготовить мобильную версию";Commit();}
   if(e.Args.Contains("--settings"))Journal.ShowSettings();
   Journal.Icon=BrandAssets.WindowIcon();
   tray = new System.Windows.Forms.NotifyIcon { Text = L.T("Silk Quest · Журнал странника"), Icon = BrandAssets.TrayIcon(), Visible = true };
   var menu = new System.Windows.Forms.ContextMenuStrip();
   menu.Items.Add(L.T("Открыть журнал"), null, (_,_) => Dispatcher.Invoke(ShowJournal));
   menu.Items.Add(L.T("Показать / скрыть висящий квест"), null, (_,_) => Dispatcher.Invoke(() => { if (Overlay == null) Pin(State.SelectedId); else HideOverlay(); }));
   menu.Items.Add(L.T("Выйти"), null, (_,_) => Dispatcher.Invoke(ExitApp)); tray.ContextMenuStrip = menu;
   tray.DoubleClick += (_,_) => Dispatcher.Invoke(ShowJournal);
   timer.Interval = TimeSpan.FromMilliseconds(250); timer.Tick += (_,_) => TimerTick(); timer.Start();
   if (State.OverlayVisible && State.Find(State.PinnedId) != null) Pin(State.PinnedId);
   if (Store.RecoveryMessage != null) MessageBox.Show(Store.RecoveryMessage, L.T("Восстановление журнала"));
   if (e.Args.Contains("--verify-ui")) Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
    try { SelfTest.VerifyUI(this); File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"ui-test-passed.txt"),"Native UI checks passed: Topmost OS flag, move, resize, opacity, compact mode, task sync, focus pause, hidden journal, overlay restore.\n"+DateTime.Now.ToString("O")); ExitApp(); }
    catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"ui-test-failed.txt"),ex.ToString());ExitApp();Environment.ExitCode=1;}
   }));
   if(e.Args.Contains("--verify-phone"))Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async()=>{
    try{await PhoneTests.Run(this);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"phone-test-passed.txt"),"HTTPS pairing, fingerprint pinning, auth, long poll, quest pin, offline retry, duplicate completion, reward consistency, stale pin rejection, revocation passed.");}
    catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"phone-test-failed.txt"),ex.ToString());Environment.ExitCode=1;}finally{ExitApp();}
   }));
  }
  catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"error.log"),ex.ToString()); MessageBox.Show(ex.Message, L.T("Не удалось открыть журнал")); ExitApp(); }
 }
 public void ShowJournal() { Journal.Show(); Journal.WindowState = WindowState.Normal; Journal.Activate(); }
 public bool Commit(bool redraw = true,bool notifyFailure=true)
 {
  Journal?.FlushEditors();
  Journal?.CaptureViewState();
  State.Reconcile();
  try { Store.Save(State); SaveHealthy = true; }
  catch (Exception ex) { bool previouslyHealthy=SaveHealthy;SaveHealthy=false;if(notifyFailure&&previouslyHealthy)MessageBox.Show(L.T("Не удалось сохранить изменения. Оставь приложение открытым и проверь доступ к папке.\n") + ex.Message,L.T("Ошибка сохранения")); }
  Journal?.UpdateSaveIndicator();if(SaveHealthy)Phone?.Publish();
  if (redraw) Changed?.Invoke();
  return SaveHealthy;
 }
 public void Select(string id) { State.SelectedId = id; Commit(); }
 public bool Complete(Quest q, bool value,bool notifyFailure=true)
 {
  Journal.FlushEditors();State.Reconcile();if(q.Done==value)return Commit(false,notifyFailure);var before=UndoAction.Clone(State);bool wasRunning=Running;
  int earned=State.CompleteQuest(q,value);
  if (value && State.FocusQuestId != null && q.Walk().Any(x=>x.Id==State.FocusQuestId)) Running = false;
  if(!Commit(false,notifyFailure)){State=before;Running=wasRunning;RefreshViews();Journal.Toast(L.T("Не удалось сохранить действие. Оно не подтверждено."));return false;}
  RememberUndo(UndoAction.Completion(before,State,earned));RefreshViews();
  Journal.Toast(value?(earned>0?L.F("✧  Квест завершён · +{0}",L.Beads(earned)):L.T("✧  Стежок за стежком. Квест завершён.")):L.T("Квест возвращён в работу."),true);
  if(value){if(earned>0)RewardEarned?.Invoke(earned);if(State.Sound)System.Media.SystemSounds.Asterisk.Play();}return true;
 }
 public void Pin(string? id)
 {
  if (State.Find(id) == null) { Journal.Toast(L.T("Сначала выбери квест.")); return; }
  State.PinnedId = id; State.OverlayVisible = true;
  if (Overlay == null) { Overlay = new OverlayWindow(this); Overlay.Show(); } else Overlay.Refresh();
  Commit();
 }
 public void SetPhoneSync(bool enabled)
 {
  State.PhoneSyncEnabled=enabled;if(enabled)Phone.Start();else Phone.Stop();
  if(enabled&&!Phone.Listening)State.PhoneSyncEnabled=false;Commit();
 }
 public void PinPhone(string? id)
 {
  if(id!=null&&State.Find(id)==null)return;
  State.PhonePinnedId=id;State.PhonePinGeneration=Guid.NewGuid().ToString("N");Commit();
  if(id!=null&&(!Phone.Listening||State.PhoneToken==null)){Journal.ShowSettings();Journal.Toast(L.T("Квест выбран. Подключи Android в настройках Wi-Fi."));}
  else Journal.Toast(id==null?L.T("Квест снят с телефона."):L.T("Квест закреплён на телефоне. Обновится при связи по Wi-Fi."));
 }
 public void HideOverlay()
 {
  State.OverlayVisible = false; var old = Overlay; Overlay = null; old?.Close(); Commit();
 }
 public void OverlayClosed(OverlayWindow window)
 {
  if (Overlay == window) { Overlay = null; State.OverlayVisible = false; Commit(); }
 }
 public void ToggleTimer(string? id)
 {
  var q = State.Find(id); if (q == null || q.Done) return;
  if (State.FocusQuestId != id) { State.FocusQuestId = id; State.FocusSessionMinutes=State.FocusMinutes; State.FocusSeconds = State.FocusMinutes * 60; Running = false; }
  if (Running) { State.FocusSeconds = Math.Max(0, (int)Math.Ceiling((focusDeadline-DateTime.UtcNow).TotalSeconds)); Running = false; }
  else { if (State.FocusSeconds <= 0) {State.FocusSessionMinutes=State.FocusMinutes;State.FocusSeconds = State.FocusMinutes * 60;} focusDeadline = DateTime.UtcNow.AddSeconds(State.FocusSeconds); Running = true; }
  Commit(); Tick?.Invoke();
 }
 public void ResetTimer(bool redraw = true) { Running = false; State.FocusSessionMinutes=State.FocusMinutes;State.FocusSeconds = State.FocusMinutes*60; Commit(redraw); Tick?.Invoke(); }
 public int FocusDuration(string? id)=>60*(State.FocusQuestId==id&&State.FocusSessionMinutes>0?State.FocusSessionMinutes:State.FocusMinutes);
 public bool SetFocusMinutes(int minutes,bool notifyFailure=true)
 {
  minutes=Math.Clamp(minutes,1,180);if(State.FocusMinutes==minutes)return true;
  int before=State.FocusMinutes,session=State.FocusSessionMinutes,seconds=State.FocusSeconds;
  bool fresh=!Running&&(State.FocusQuestId==null||seconds<=0||seconds==FocusDuration(State.FocusQuestId));
  State.FocusSessionMinutes=fresh?minutes:session>0?session:before;State.FocusMinutes=minutes;
  if(fresh)State.FocusSeconds=minutes*60;
  if(!Commit(false,notifyFailure)){State.FocusMinutes=before;State.FocusSessionMinutes=session;State.FocusSeconds=seconds;Tick?.Invoke();return false;}
  Tick?.Invoke();return true;
 }
 void TimerTick()
 {
  if (!Running) return;
  int seconds = Math.Max(0,(int)Math.Ceiling((focusDeadline-DateTime.UtcNow).TotalSeconds));
  if (seconds == State.FocusSeconds) return;
  State.FocusSeconds = seconds;
  if (seconds == 0)
  {
   Running = false; Commit(); Journal.Toast(L.T("🔔  Время фокуса завершено. Можно отдохнуть."));
   if (State.Sound) System.Media.SystemSounds.Asterisk.Play();
   tray?.ShowBalloonTip(3500,L.T("Журнал странника"),L.T("Время фокуса завершено. Можно отдохнуть."),System.Windows.Forms.ToolTipIcon.Info);
  }
  else if (seconds % 15 == 0) Commit(false);
  Tick?.Invoke();
 }
 public string TimerLabel(string? id) { int s = State.FocusQuestId == id ? State.FocusSeconds : State.FocusMinutes*60; return $"{s/60:00}:{s%60:00}"; }
 public void ExitApp()
 {
  if(State!=null&&Store!=null&&!Commit(false)){Journal?.Show();Journal?.Toast(L.T("Журнал остаётся открытым: изменения не удалось сохранить."));return;}
  Exiting = true; timer.Stop();
  Phone?.Dispose();tray?.Dispose(); Overlay?.Close(); Journal?.Close(); Shutdown();
 }
 protected override void OnExit(ExitEventArgs e) { Phone?.Dispose();tray?.Dispose(); mutex?.Dispose(); base.OnExit(e); }
}
