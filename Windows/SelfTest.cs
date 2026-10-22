using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SilkJournal;

static class SelfTest
{
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr window,int index);
 public static void VerifyUI(App app)
 {
  void Check(bool v,string message){if(!v)throw new Exception(message);}
  void Pump(int ms){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};t.Tick+=(_,_)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
  string language=L.Language;L.Language="ru";
  foreach(var sample in new[]{(0L,"бусин"),(1L,"бусина"),(2L,"бусины"),(5L,"бусин"),(11L,"бусин"),(14L,"бусин"),(21L,"бусина"),(22L,"бусины"),(111L,"бусин")})
   Check(L.Beads(sample.Item1).EndsWith(" "+sample.Item2),"Russian currency agreement: "+sample.Item1);
  Check(L.Beads(1,true)=="1 бусины"&&L.Beads(2,true)=="2 бусин","Genitive currency agreement");
  L.Language="en";Check(L.F("Не хватает {0}",L.Beads(2,true))=="You need another 2 rosaries","English currency translation");L.Language=language;
  Check(app.Journal.RewardText=="0","Zero balance visible at startup");
  var q=app.State.Quests[0].Children[2];app.Pin(q.Id);var window=app.Overlay!;
  Check(window.IsVisible&&window.Topmost,"Overlay visible and topmost");
  Check((GetWindowLong(new WindowInteropHelper(window).Handle,-20)&8)!=0,"Windows WS_EX_TOPMOST flag");
  window.Left=120;window.Top=140;window.Width=390;window.Height=320;
  Check(app.State.OverlayLeft==120&&app.State.OverlayWidth==390,"Bounds persistence");
  Pump(450);var beforeMove=File.ReadAllBytes(app.Store.FilePath);
  for(int i=0;i<20;i++)window.Left=121+i;
  Check(File.ReadAllBytes(app.Store.FilePath).SequenceEqual(beforeMove),"Moving window does not synchronously write every position to disk");
  Pump(450);Check(app.Store.Load().OverlayLeft==window.Left,"Settled position is saved after debounce");
  app.State.OverlayOpacity=.72;window.ApplyAppearance();Check(Math.Abs(window.Opacity-.72)<.001,"Opacity");
  app.State.OverlayCompact=true;window.ApplyAppearance();Pump(100);double compactHeight=window.Height;Check(compactHeight>=110&&compactHeight<160,"Compact fits content");
  app.State.OverlayCompact=false;window.ApplyAppearance();Pump(100);Check(window.Height>compactHeight&&window.Height<250,"Expanded fits content without blank footer");
  app.Journal.Hide();Check(window.IsVisible,"Overlay survives hidden main window");app.ShowJournal();
  app.ToggleTimer(q.Id);Check(app.Running&&app.Journal.FocusSpoolRunning,"Focus starts spool animation");
  double initialAngle=app.Journal.FocusSpoolAngle;var frame=new DispatcherFrame();var sampleTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(220)};sampleTimer.Tick+=(_,_)=>{sampleTimer.Stop();frame.Continue=false;};sampleTimer.Start();Dispatcher.PushFrame(frame);
  Check(Math.Abs(app.Journal.FocusSpoolAngle-initialAngle)>1,"Animation clock actually advances thread phase");
  app.Journal.Hide();Pump(100);Check(app.Running&&!app.Journal.FocusSpoolRunning,"Hidden journal stops decorative animation while focus continues");
  app.ShowJournal();Pump(100);Check(app.Running&&app.Journal.FocusSpoolRunning,"Visible journal resumes decorative animation");
  app.ToggleTimer(q.Id);Check(!app.Running&&!app.Journal.FocusSpoolRunning,"Focus pause stops spool animation");
  int previousSeconds=app.State.FocusSeconds;
  app.State.FocusSeconds=5;app.RefreshViews();Check(app.Journal.FocusSpoolWinding==0&&app.Journal.FocusSpoolLooseThread==1,"Last five seconds show bare spool and full free strand");
  app.State.FocusSeconds=2;app.RefreshViews();Check(app.Journal.FocusSpoolWinding==0&&Math.Abs(app.Journal.FocusSpoolLooseThread-.4)<.001,"Free strand is consumed during final seconds");
  app.State.FocusSeconds=0;app.RefreshViews();Check(app.Journal.FocusSpoolWinding==0&&app.Journal.FocusSpoolLooseThread==0,"Focus end removes all thread");
  app.State.FocusSeconds=previousSeconds;app.RefreshViews();Check(app.Journal.FocusSpoolWinding>.9,"Restoring early session restores winding");
  Check(app.Journal.Icon!=null,"Application icon resource loaded");app.SetWeathering(false);Check(!app.Store.Load().Weathering,"Independent weathering setting persists");app.SetWeathering(true);
  app.Complete(q,true);Check(q.Done&&q.Children.All(x=>x.Done),"Complete from shared controller");app.Complete(q,false);
  app.Commit();var loaded=app.Store.Load();Check(loaded.PinnedId==q.Id&&loaded.OverlayVisible&&loaded.OverlayLeft==window.Left,"Overlay state roundtrip");
  window.Left+=11;double finalLeft=window.Left;app.HideOverlay();Check(!window.IsVisible,"Overlay closes cleanly");
  Check(app.Store.Load().OverlayLeft==finalLeft,"Closing before debounce preserves the latest position");
  app.Pin(q.Id);Check(app.Overlay!.IsVisible&&Math.Abs(app.Overlay.Opacity-.72)<.001,"Overlay restores");app.HideOverlay();
  Pump(300);double start=app.Journal.HighlightPosition;app.Journal.NavigateRelative(2);Check(app.Journal.CurrentPage=="Путешествие","Relative tab navigation");Pump(80);double middle=app.Journal.HighlightPosition;Pump(300);double end=app.Journal.HighlightPosition;
  Check(middle>start&&middle<end,"Selection moves through intermediate positions");
  app.Journal.NavigateRelative(-3);Check(app.Journal.CurrentPage=="Настройки","Tab navigation wraps");
  app.State.Motion=false;app.Journal.NavigateRelative(1);Pump(50);Check(app.Journal.CurrentPage=="Сегодня"&&Math.Abs(app.Journal.HighlightPosition-start)<1,"Motion disabled gives immediate selection");app.State.Motion=true;
  FocusSettingsTests.Run(app,Pump);
  PolishTests.UI(app,Pump);
  ShopTests.Run(app,Pump);
 }
 public static void Run()
 {
  int count=0;
  void Check(bool value,string label){if(!value)throw new Exception(label);count++;}
  var starter=JournalState.Starter();
  Check(starter.Language=="en"&&starter.Quests.Count==3&&starter.Quests[0].Title=="Launch a portfolio","Fresh journals start in English with the example quests");
  Check(starter.Quests.SelectMany(q=>q.Walk()).All(q=>!q.Title.Any(c=>c>='А'&&c<='я')&&!q.Notes.Any(c=>c>='А'&&c<='я')),"Starter quest names and notes are English");
  Check(starter.ShopItems.Count==1&&starter.ShopItems[0].Name=="Pepsi Bottle"&&starter.ShopItems[0].Price==15&&ShopImages.Preview(starter.ShopItems[0])!=null,"Starter shop includes the translated Pepsi Bottle and embedded artwork");
  Check(starter.Rosaries==0&&starter.ShopPurchases.Count==0&&!starter.PhoneSyncEnabled&&starter.PhoneToken==null,"Starter state contains no user balance, purchases or pairing");
  var rewards=JournalState.Demo();rewards.InitializeRewards();
  Check(rewards.Rosaries==0,"Existing completed steps start without retroactive rewards");
  var rewardRoot=rewards.Quests[0];var rewardLeaf=rewardRoot.Children[2].Children[1];
  Check(rewards.CompleteQuest(rewardLeaf,true)==5&&rewards.Rosaries==5,"First leaf earns five rosaries");
  rewards.CompleteQuest(rewardLeaf,false);Check(rewards.CompleteQuest(rewardLeaf,true)==0&&rewards.Rosaries==5,"Reopening cannot farm rewards");
  Check(rewards.CompleteQuest(rewardRoot,true)==60&&rewards.Rosaries==65,"Parent awards unpaid steps and completion bonuses");
  rewards.CompleteQuest(rewardRoot,false);Check(rewards.CompleteQuest(rewardRoot,true)==0,"Whole quest cannot be farmed");
  var roundtrip=JsonSerializer.Deserialize<JournalState>(JsonSerializer.Serialize(rewards))!;
  Check(roundtrip.Rosaries==65&&roundtrip.RewardedQuestIds.SetEquals(rewards.RewardedQuestIds),"Reward balance and claims persist");
  var settings=new JournalState{MainReward=40,SideReward=9,StepReward=3};var main=new Quest{Children=new(){new Quest(),new Quest()}};var side=new Quest();settings.Quests.Add(main);settings.Quests.Add(side);settings.InitializeRewards();
  Check(settings.KindOf(main)==QuestKind.Main&&settings.KindOf(side)==QuestKind.Side&&settings.KindOf(main.Children[0])==QuestKind.Step,"Automatic kinds match hierarchy");
  main.Children[0].CustomReward=17;Check(settings.PendingReward(main)==60,"Preview includes custom amount and main bonus");
  Check(settings.CompleteQuest(main,true)==60,"Configured bases and custom amount pay correctly");
  side.Kind=QuestKind.Main;side.CustomReward=0;Check(settings.CompleteQuest(side,true)==0,"Zero custom reward overrides kind base");settings.CompleteQuest(side,false);side.CustomReward=100;Check(settings.CompleteQuest(side,true)==0,"Changing reward cannot re-award a claimed quest");
  var savedSettings=JsonSerializer.Deserialize<JournalState>(JsonSerializer.Serialize(settings))!;Check(savedSettings.MainReward==40&&savedSettings.SideReward==9&&savedSettings.StepReward==3&&savedSettings.Quests[0].Children[0].CustomReward==17&&savedSettings.Quests[1].Kind==QuestKind.Main,"Kinds, bases and custom rewards persist");
  var s=JournalState.Demo();var root=s.Quests[0];var child=root.Children[2];
  Check(s.Find(child.Id)==child,"Nested lookup");Check(Math.Abs(root.Progress-.5)<.001,"Leaf progress");
  root.SetDone(true);s.Reconcile();Check(root.Walk().All(q=>q.Done),"Recursive completion");
  child.Children[0].Done=false;s.Reconcile();Check(!root.Done&&!child.Done,"Reopen ancestor");
  Check(s.Find("missing")==null,"Unknown quest");
  string folder=Path.Combine(AppContext.BaseDirectory,"test-data-"+Guid.NewGuid().ToString("N"));
  var store=new JournalStore(folder);s.PinnedId=child.Id;s.OverlayLeft=123;s.OverlayOpacity=.73;s.OverlayVisible=true;store.Save(s);
  var loaded=store.Load();Check(loaded.PinnedId==child.Id&&loaded.OverlayVisible,"Overlay restored");Check(loaded.OverlayLeft==123&&loaded.OverlayOpacity==.73,"Overlay appearance persisted");
  loaded.Quests[0].Title="Изменённый квест";store.Save(loaded);Check(File.Exists(store.FilePath+".bak"),"Atomic backup");
  Check(store.Load().Quests[0].Title=="Изменённый квест","Unicode roundtrip");
  File.WriteAllText(store.FilePath,"{broken");var recovered=store.Load();Check(store.RecoveryMessage!=null&&recovered.Quests.Count==3,"Backup recovery");Check(Directory.GetFiles(folder,"*.damaged-*").Length==1,"Corrupt file preserved");
  var legacy=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(recovered))!.AsObject();legacy.Remove("FocusSessionMinutes");legacy["FocusMinutes"]=37;legacy["FocusSeconds"]=2000;File.WriteAllText(store.FilePath,legacy.ToJsonString());
  var migrated=store.Load();Check(migrated.FocusMinutes==37&&migrated.FocusSessionMinutes==37&&migrated.FocusSeconds==2000,"Older journals retain timer duration and remaining time after migration");
  PolishTests.Models(Check);
  MobileSyncTests.Models(Check);
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test-passed.txt"),$"{count} checks passed\n"+DateTime.Now.ToString("O"));
  // Tests use only their freshly created folder, and retain it for inspection.
 }
}
