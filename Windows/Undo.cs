using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SilkJournal;

sealed class UndoAction
{
 public string Label {get;}
 public DateTime Expires {get;}=DateTime.UtcNow.AddSeconds(10);
 readonly Func<JournalState,bool> apply;
 public UndoAction(string label,Func<JournalState,bool> apply){Label=label;this.apply=apply;}
 public bool Apply(JournalState state)=>DateTime.UtcNow<Expires&&apply(state);
 public static JournalState Clone(JournalState state)=>JsonSerializer.Deserialize<JournalState>(JsonSerializer.Serialize(state))!;
 public static UndoAction Completion(JournalState before,JournalState after,int earned)
 {
  var old=before.Quests.SelectMany(q=>q.Walk()).ToDictionary(q=>q.Id);
  var changes=after.Quests.SelectMany(q=>q.Walk()).Where(q=>old.TryGetValue(q.Id,out var p)&&(q.Done!=p.Done||q.CompletionStamp!=p.CompletionStamp)).Select(q=>(q.Id,Before:old[q.Id].Done,After:q.Done,Stamp:q.CompletionStamp)).ToArray();
  var claims=after.RewardedQuestIds.Except(before.RewardedQuestIds).ToArray();
  return new UndoAction("Отменить изменение квеста",s=>{
   if(s.Rosaries<earned||changes.Any(c=>s.Find(c.Id) is not Quest q||q.Done!=c.After||q.CompletionStamp!=c.Stamp))return false;
   foreach(var c in changes){var q=s.Find(c.Id)!;q.Done=c.Before;q.CompletionStamp=Guid.NewGuid().ToString("N");}
   s.Rosaries-=earned;foreach(string id in claims)s.RewardedQuestIds.Remove(id);s.Reconcile();return true;
  });
 }
 public static UndoAction Deletion(JournalState before,Quest removed,string? parentId,int index)
 {
  before=Clone(before);
  var copy=JsonSerializer.Deserialize<Quest>(JsonSerializer.Serialize(removed))!;var ids=copy.Walk().Select(q=>q.Id).ToHashSet();
  return new UndoAction("Вернуть удалённый квест",s=>{
   if(ids.Any(id=>s.Find(id)!=null))return false;var parent=parentId==null?null:s.Find(parentId);if(parentId!=null&&parent==null)return false;
   var list=parent?.Children??s.Quests;list.Insert(Math.Min(index,list.Count),copy);s.SelectedId=ids.Contains(before.SelectedId??"")?before.SelectedId:copy.Id;
   foreach(var id in before.ExpandedQuestIds.Where(ids.Contains))s.ExpandedQuestIds.Add(id);
   if(s.PinnedId==null&&ids.Contains(before.PinnedId??"")){s.PinnedId=before.PinnedId;s.OverlayVisible=before.OverlayVisible;}
   if(s.PhonePinnedId==null&&ids.Contains(before.PhonePinnedId??"")){s.PhonePinnedId=before.PhonePinnedId;s.PhonePinGeneration=Guid.NewGuid().ToString("N");}
   if(s.FocusQuestId==null&&ids.Contains(before.FocusQuestId??"")){s.FocusQuestId=before.FocusQuestId;s.FocusSeconds=before.FocusSeconds;s.FocusSessionMinutes=before.FocusSessionMinutes;}
   s.Reconcile();return true;
  });
 }
}

public partial class App
{
 UndoAction? undo;
 public bool CanUndo=>undo!=null&&DateTime.UtcNow<undo.Expires;
 public string UndoLabel=>L.T(undo?.Label??"Отменить");
 public event Action? UndoChanged;
 public event Action<int>? RewardEarned;
 void RememberUndo(UndoAction action){undo=action;UndoChanged?.Invoke();}
 public bool Undo()
 {
  Journal.FlushEditors();var before=UndoAction.Clone(State);var action=undo;
  if(action==null||!action.Apply(State)){undo=null;UndoChanged?.Invoke();Journal.Toast(L.T("Это действие уже изменено или время отмены истекло."));return false;}
  Journal.CancelEditors();Journal.ReloadViewPreferences();if(!Commit(false)){State=before;RefreshViews();return false;}
  undo=null;UndoChanged?.Invoke();SyncOverlay();RefreshViews();Journal.Toast(L.T("Действие отменено."));return true;
 }
 public bool DeleteQuest(Quest quest)
 {
  Journal.FlushEditors();var before=UndoAction.Clone(State);var ids=quest.Walk().Select(q=>q.Id).ToHashSet();
  var parent=State.Quests.SelectMany(q=>q.Walk()).FirstOrDefault(q=>q.Children.Contains(quest));var list=parent?.Children??State.Quests;int index=list.IndexOf(quest);if(index<0)return false;
  var action=UndoAction.Deletion(before,quest,parent?.Id,index);list.RemoveAt(index);Journal.CancelEditors();
  if(ids.Contains(State.PinnedId??"")){State.PinnedId=null;State.OverlayVisible=false;}
  if(ids.Contains(State.PhonePinnedId??"")){State.PhonePinnedId=null;State.PhonePinGeneration=Guid.NewGuid().ToString("N");}
  if(ids.Contains(State.FocusQuestId??"")){Running=false;State.FocusQuestId=null;State.FocusSessionMinutes=State.FocusMinutes;State.FocusSeconds=State.FocusMinutes*60;}
  State.SelectedId=parent?.Id??State.Quests.FirstOrDefault()?.Id;
  if(!Commit(false)){State=before;RefreshViews();return false;}
  RememberUndo(action);SyncOverlay();RefreshViews();Journal.Toast(L.T("Квест удалён."),true);return true;
 }
 void SyncOverlay()
 {
  if(!State.OverlayVisible||State.Find(State.PinnedId)==null){var old=Overlay;Overlay=null;old?.Close();}
  else if(Overlay==null){Overlay=new OverlayWindow(this);Overlay.Show();}else Overlay.Refresh();
 }
 public void RefreshViews()=>Changed?.Invoke();
 public bool RestoreSnapshot(string path)
 {
  Journal.FlushEditors();var source=Store.ReadSnapshot(path);if(!Commit(false))return false;var before=UndoAction.Clone(State);
  State.Quests=source.Quests;State.ShopItems=source.ShopItems;State.ShopPurchases=source.ShopPurchases;State.Rosaries=source.Rosaries;State.RewardedQuestIds=source.RewardedQuestIds;State.RewardsInitialized=source.RewardsInitialized;
  State.SelectedId=State.Find(source.SelectedId)?.Id??State.Quests.FirstOrDefault()?.Id;State.ExpandedQuestIds=source.ExpandedQuestIds;State.CollapsedQuestIds=source.CollapsedQuestIds;
  if(State.Find(State.PinnedId)==null){State.PinnedId=null;State.OverlayVisible=false;}
  if(State.Find(State.PhonePinnedId)==null)State.PhonePinnedId=null;State.PhonePinGeneration=Guid.NewGuid().ToString("N");
  Running=false;if(State.Find(State.FocusQuestId)==null)State.FocusQuestId=null;Journal.CancelEditors();Journal.ReloadViewPreferences();
  if(!Commit(false)){State=before;RefreshViews();return false;}
  undo=null;UndoChanged?.Invoke();SyncOverlay();Journal.ReloadViewPreferences();RefreshViews();Journal.Toast(L.T("Квесты, лавка и баланс восстановлены. Настройки подключения сохранены."));return true;
 }
}
