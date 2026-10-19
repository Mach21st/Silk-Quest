using System;
using System.IO;
using System.Linq;

namespace SilkJournal;

static class PolishTests
{
 public static void Models(Action<bool,string> check)
 {
  var s=JournalState.Demo();s.InitializeRewards();var before=UndoAction.Clone(s);var group=s.Quests[0].Children[2];int earned=s.CompleteQuest(group,true);var undo=UndoAction.Completion(before,s,earned);group.Notes="Keep a later edit";
  check(undo.Apply(s)&&!group.Done&&group.Children[0].Done&&s.Rosaries==0&&group.Notes=="Keep a later edit","Undo restores nested completion and balance without overwriting edits");
  check(s.CompleteQuest(group,true)==earned,"Undone reward can be earned once again");
  var earlier=UndoAction.Clone(s);s.CompleteQuest(group,false);var stale=UndoAction.Completion(earlier,s,0);group.SetDone(true);s.Reconcile();check(!stale.Apply(s),"Undo refuses subsequent changes");
  s.PhonePinnedId=group.Id;s.FocusQuestId=group.Id;s.ExpandedQuestIds.Add(group.Id);var deletion=UndoAction.Deletion(s,group,s.Quests[0].Id,2);s.Quests[0].Children.Remove(group);s.PhonePinnedId=null;s.FocusQuestId=null;s.ExpandedQuestIds.Clear();
  check(deletion.Apply(s)&&s.Find(group.Id)!=null&&s.PhonePinnedId==group.Id&&s.FocusQuestId==group.Id&&s.ExpandedQuestIds.Contains(group.Id),"Deletion undo restores subtree and pins");
  string folder=Path.Combine(AppContext.BaseDirectory,"test-data-snapshots-"+Guid.NewGuid().ToString("N"));var date=new DateTime(2026,10,1);var store=new JournalStore(folder,()=>date);store.Save(s);s.Quests[0].Title="Second save";store.Save(s);string first=store.Snapshots().First(p=>p.Path.EndsWith("2026-10-01.json")).Path;string original=File.ReadAllText(first);s.Quests[0].Title="Third save";store.Save(s);
  check(File.ReadAllText(first)==original,"Daily snapshot stays stable during same day");
  for(int i=1;i<=9;i++){date=date.AddDays(1);s.Quests[0].Title="Day "+i;store.Save(s);}
  var snapshots=store.Snapshots();check(snapshots.Length==8&&snapshots.Count(p=>Path.GetDirectoryName(p.Path)==Path.Combine(folder,"snapshots"))==7&&!File.Exists(first),"Daily history capped at seven plus previous save");
  check(store.ReadSnapshot(snapshots[1].Path).Quests.Count==s.Quests.Count,"Snapshot validates and loads");
  bool rejected=false;try{store.ReadSnapshot(Path.Combine(folder,"outside.json"));}catch(InvalidDataException){rejected=true;}check(rejected,"Restore rejects a path outside known snapshots");
  s.CurrentPage="Путешествие";s.PageScrollOffsets["Настройки"]=321;s.DetailScrollOffsets["test"]=99;store.Save(s);var round=store.Load();check(round.CurrentPage=="Путешествие"&&round.PageScrollOffsets["Настройки"]==321&&round.ExpandedQuestIds.Contains(group.Id),"View preferences persist with journal");
 }
 public static void UI(App app,Action<int> pump)
 {
  void Check(bool yes,string label){if(!yes)throw new Exception(label);}
  var q=new Quest{Title="Undo verification"};app.State.Quests.Add(q);app.Commit();long balance=app.State.Rosaries;app.Complete(q,true);Check(app.Journal.UndoVisible,"Undo action exposed in footer");q.Notes="Later note";app.Commit(false);
  Check(app.Undo()&&!app.State.Find(q.Id)!.Done&&app.State.Rosaries==balance&&app.State.Find(q.Id)!.Notes=="Later note","Controller completion undo preserves later note");
  q=app.State.Find(q.Id)!;app.State.PhonePinnedId=q.Id;app.Commit();app.DeleteQuest(q);Check(app.State.Find(q.Id)==null,"Controller deletion removes quest");Check(app.Undo()&&app.State.Find(q.Id)!=null&&app.State.PhonePinnedId==q.Id,"Controller deletion undo restores phone pin");
  var task=app.State.Find(q.Id)!;task.Title="Before restore";app.Commit(false);task.Title="After restore";app.Commit(false);string token=app.State.PhoneToken??"test-current-credentials";app.State.PhoneToken=token;Check(app.RestoreSnapshot(app.Store.FilePath+".bak"),"Controller snapshot restore succeeds");Check(app.State.Find(q.Id)!.Title=="Before restore"&&app.State.PhoneToken==token,"Restore changes quests while preserving current credentials");
  app.Journal.ShowSettings();pump(200);app.Journal.SetTaskScroll(260);pump(120);app.Commit(false);double offset=app.Journal.TaskScrollOffset;Check(offset>200,"Settings can scroll");app.Journal.NavigateRelative(1);pump(180);app.Journal.NavigateRelative(-1);pump(180);Check(Math.Abs(app.Journal.TaskScrollOffset-offset)<2,"Per-tab scroll restored");app.Commit();pump(180);Check(Math.Abs(app.Journal.TaskScrollOffset-offset)<2,"Redraw preserves scroll");
  var loaded=app.Store.Load();Check(loaded.CurrentPage=="Настройки"&&Math.Abs(loaded.PageScrollOffsets["Настройки"]-offset)<2,"Last tab and scroll saved on disk");
  string questsBefore=System.Text.Json.JsonSerializer.Serialize(app.State.Quests);long rosaries=app.State.Rosaries;string? phoneToken=app.State.PhoneToken;
  app.SetLanguage("en");pump(150);Check(app.Store.Load().Language=="en"&&app.Journal.Title=="Wanderer's Journal · Silk Journal","English is applied immediately and saved");
  Check(L.F("Код действует ещё {0} мин",3)=="Code expires in 3 min","Dynamic English labels formatted");
  Check(L.T("Фокус")=="Focus"&&L.T("Длительность фокуса в минутах")=="Focus duration in minutes","Focus settings translated");
  var counter=new RosaryCounter();L.Language="ru";counter.SetAmount(12000,false);L.Language="en";counter.SetAmount(12000,false);Check(counter.Text=="12,000","Unchanged balance reformats immediately when language changes");
  Check(System.Text.Json.JsonSerializer.Serialize(app.State.Quests)==questsBefore&&app.State.Rosaries==rosaries&&app.State.PhoneToken==phoneToken,"Language switch preserves quest text, rewards and pairing");
  app.Journal.MinWidth=800;app.Journal.MinHeight=480;app.Journal.Width=960;app.Journal.Height=620;pump(200);Check(app.Journal.ActualWidth<=961&&app.Journal.ActualHeight<=621,"English compact layout fits reduced logical work area");
  app.SetLanguage("ru");pump(100);Check(app.Store.Load().Language=="ru"&&L.T("Настройки")=="Настройки","Switching back to Russian is saved");
 }
}
