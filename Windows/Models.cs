using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SilkJournal;

public enum QuestKind { Auto, Main, Side, Step }
public sealed class Quest
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Title { get; set; } = L.T("Новый квест");
 public string Notes { get; set; } = "";
 public bool Done { get; set; }
 public string CompletionStamp {get;set;} = "";
 public bool Today { get; set; } = true;
 public QuestKind Kind { get; set; }
 public int? CustomReward { get; set; }
 public List<Quest> Children { get; set; } = new();
 public IEnumerable<Quest> Walk() { yield return this; foreach (var c in Children) foreach (var q in c.Walk()) yield return q; }
 public IEnumerable<Quest> Leaves() => Children.Count == 0 ? new[] { this } : Children.SelectMany(c => c.Leaves());
 public double Progress => Leaves().Count(q => q.Done) / (double)Leaves().Count();
 public void SetDone(bool value) { if(Done!=value)CompletionStamp=Guid.NewGuid().ToString("N");Done = value; foreach (var q in Children) q.SetDone(value); }
 public void Reconcile() { foreach (var q in Children) q.Reconcile(); if (Children.Count > 0){bool value=Children.All(q => q.Done);if(Done!=value)CompletionStamp=Guid.NewGuid().ToString("N");Done=value;} }
}
public sealed partial class JournalState
{
 public int Version { get; set; } = 1;
 public string Language {get;set;} = "ru";
 public List<Quest> Quests { get; set; } = new();
 public List<ShopItem> ShopItems {get;set;} = new();
 public List<ShopPurchase> ShopPurchases {get;set;} = new();
 public string? SelectedId { get; set; }
 public string CurrentPage {get;set;} = "Сегодня";
 public HashSet<string> ExpandedQuestIds {get;set;} = new();
 public HashSet<string> CollapsedQuestIds {get;set;} = new();
 public Dictionary<string,double> PageScrollOffsets {get;set;} = new();
 public Dictionary<string,double> DetailScrollOffsets {get;set;} = new();
 public string? PinnedId { get; set; }
 public bool PhoneSyncEnabled { get; set; }
 public string? PhonePinnedId { get; set; }
 public string PhonePinGeneration { get; set; } = Guid.NewGuid().ToString("N");
 public string? PhoneToken { get; set; }
 public string? PhoneDeviceName { get; set; }
 public int PhonePort { get; set; } = 38921;
 public bool OverlayVisible { get; set; }
 public double OverlayLeft { get; set; } = -1;
 public double OverlayTop { get; set; } = -1;
 public double OverlayWidth { get; set; } = 370;
 public double OverlayHeight { get; set; } = 290;
 public double OverlayOpacity { get; set; } = 0.96;
 public double OverlayFontSize { get; set; } = 21;
 public bool OverlayCompact { get; set; }
 public bool Sound { get; set; }
 public bool Motion { get; set; } = true;
 public bool Weathering { get; set; } = true;
 public long Rosaries { get; set; }
 public int MainReward { get; set; } = 25;
 public int SideReward { get; set; } = 5;
 public int StepReward { get; set; } = 5;
 public QuestKind KindOf(Quest q)=>q.Kind!=QuestKind.Auto?q.Kind:q.Children.Count>0?QuestKind.Main:Quests.Contains(q)?QuestKind.Side:QuestKind.Step;
 public int RewardFor(Quest q)=>Math.Clamp(q.CustomReward??(KindOf(q) switch {QuestKind.Main=>MainReward,QuestKind.Side=>SideReward,_=>StepReward}),0,100000);
 public int PendingReward(Quest quest)
 {
  var leaves=quest.Leaves().Select(q=>q.Id).ToHashSet();
  return Quests.SelectMany(q=>q.Walk()).Where(q=>!q.Done&&!RewardedQuestIds.Contains(q.Id)&&q.Leaves().All(l=>l.Done||leaves.Contains(l.Id))).Sum(RewardFor);
 }
 public bool RewardsInitialized { get; set; }
 public HashSet<string> RewardedQuestIds { get; set; } = new();
 public void InitializeRewards()
 {
  if(RewardsInitialized)return;
  Reconcile();
  foreach(var q in Quests.SelectMany(q=>q.Walk()).Where(q=>q.Done))RewardedQuestIds.Add(q.Id);
  RewardsInitialized=true;
 }
 public int CompleteQuest(Quest quest,bool value)
 {
  InitializeRewards();
  var eligible=Quests.SelectMany(q=>q.Walk()).Where(q=>!q.Done).ToList();
  quest.SetDone(value);Reconcile();int earned=0;
  if(value)
  {
   foreach(var q in eligible.Where(q=>q.Done))if(RewardedQuestIds.Add(q.Id))earned+=RewardFor(q);
   foreach(var q in Quests.SelectMany(q=>q.Walk()).Where(q=>q.Done))RewardedQuestIds.Add(q.Id);
   Rosaries+=earned;
  }
  return earned;
 }
 public int FocusMinutes { get; set; } = 25;
 public int FocusSessionMinutes { get; set; }
 public string? FocusQuestId { get; set; }
 public int FocusSeconds { get; set; } = 1500;
 public Quest? Find(string? id) => Quests.SelectMany(q => q.Walk()).FirstOrDefault(q => q.Id == id);
 public void Reconcile() { foreach (var q in Quests) q.Reconcile(); }
 public static JournalState Demo()
 {
  var project = new Quest { Title = "Запустить портфолио", Notes = "Собрать место для своих лучших работ. Один маленький шаг за раз.", Children = new()
  {
   new Quest { Title = "Собрать референсы", Done = true },
   new Quest { Title = "Выбрать работы", Done = true },
   new Quest { Title = "Собрать главную страницу", Notes = "Первый экран и подборка лучших работ.", Children = new()
   {
    new Quest { Title = "Заголовок", Done = true }, new Quest { Title = "Карточки проектов" }, new Quest { Title = "Мобильная версия" }
   } }, new Quest { Title = "Опубликовать сайт" }
  } };
  return new JournalState { Quests = new() { project, new Quest { Title = "Разобрать почту", Notes = "Ответить на важное. Остальное может подождать." }, new Quest { Title = "Прогулка · 20 минут", Notes = "Отдых — часть путешествия." } }, SelectedId = project.Children[2].Id };
 }
}
public sealed class JournalStore
{
 public string FilePath { get; }
 public string? RecoveryMessage { get; private set; }
 public string? BackupWarning {get;private set;}
 readonly Func<DateTime> clock;
 static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
 public JournalStore(string folder,Func<DateTime>? clock=null) { Directory.CreateDirectory(folder); FilePath = Path.Combine(folder, "journal.json");this.clock=clock??(()=>DateTime.Now); }
 public (string Path,string Label)[] Snapshots()
 {
  var files=new List<(string Path,string Label)>();if(File.Exists(FilePath+".bak"))files.Add((FilePath+".bak",L.T("Предыдущее сохранение")));
  string folder=Path.Combine(Path.GetDirectoryName(FilePath)!,"snapshots");
  if(Directory.Exists(folder))foreach(string file in Directory.GetFiles(folder,"journal-????-??-??.json").OrderByDescending(p=>p).Take(7))
   files.Add((file,DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file)[8..],"yyyy-MM-dd",null,System.Globalization.DateTimeStyles.None,out var date)?date.ToString("dd.MM.yyyy"):Path.GetFileName(file)));
  return files.ToArray();
 }
 public JournalState ReadSnapshot(string path)
 {
  string full=Path.GetFullPath(path);if(!Snapshots().Any(s=>string.Equals(Path.GetFullPath(s.Path),full,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException(L.T("Этот снимок не принадлежит журналу."));return Read(full);
 }
 void DailySnapshot()
 {
  if(!File.Exists(FilePath))return;string folder=Path.Combine(Path.GetDirectoryName(FilePath)!,"snapshots");string destination=Path.Combine(folder,"journal-"+clock().ToString("yyyy-MM-dd")+".json");if(File.Exists(destination))return;
  try
  {
   Read(FilePath);Directory.CreateDirectory(folder);File.Copy(FilePath,destination+".tmp",true);File.Move(destination+".tmp",destination);
   foreach(string old in Directory.GetFiles(folder,"journal-????-??-??.json").OrderByDescending(p=>p).Skip(7))File.Delete(old);
   BackupWarning=null;
  }
  catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException){BackupWarning=L.T("Дневной снимок не создан: ")+ex.Message;}
 }
 public JournalState Load()
 {
  if (!File.Exists(FilePath)) return JournalState.Starter();
  try { return Read(FilePath); }
  catch (Exception ex) when (ex is JsonException || ex is IOException || ex is InvalidDataException)
  {
   string damaged = FilePath + ".damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
   File.Copy(FilePath, damaged, true);
   if (File.Exists(FilePath + ".bak"))
   {
    try { var recovered = Read(FilePath + ".bak"); RecoveryMessage = L.T("Данные восстановлены из резервной копии. Повреждённый файл сохранён рядом."); return recovered; } catch { }
   }
   throw new InvalidDataException(L.T("Не удалось прочитать журнал. Исходный файл сохранён: ") + damaged + "\n" + ex.Message);
  }
 }
 static JournalState Read(string path)
 {
  var state = JsonSerializer.Deserialize<JournalState>(File.ReadAllText(path), Json) ?? throw new InvalidDataException(L.T("Пустой журнал"));
  if (state.Version != 1 || state.Quests == null) throw new InvalidDataException(L.T("Неизвестный формат журнала"));
  state.FocusMinutes = Math.Clamp(state.FocusMinutes, 1, 180);
  state.FocusSessionMinutes = state.FocusSessionMinutes>0?Math.Clamp(state.FocusSessionMinutes,1,180):state.FocusMinutes;
  state.Language=state.Language=="en"?"en":"ru";
  state.ShopItems??=new();
  state.ShopPurchases??=new();
  state.FocusSeconds = Math.Clamp(state.FocusSeconds, 0, 10800);
  state.OverlayOpacity = Math.Clamp(state.OverlayOpacity, 0.4, 1);
  state.OverlayFontSize = Math.Clamp(state.OverlayFontSize, 15, 30);
  state.Reconcile(); state.InitializeRewards(); return state;
 }
 public void Save(JournalState state)
 {
  DailySnapshot();
  string temp = FilePath + ".tmp";
  using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){JsonSerializer.Serialize(file,state,Json);file.Flush(true);}
  if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
  else File.Move(temp, FilePath);
 }
}
