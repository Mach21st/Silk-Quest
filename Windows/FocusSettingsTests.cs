using System;
using System.IO;

namespace SilkJournal;

static class FocusSettingsTests
{
 public static void Run(App app,Action<int> pump)
 {
  void Check(bool value,string label){if(!value)throw new Exception(label);}
  int minutes=app.State.FocusMinutes,session=app.State.FocusSessionMinutes,seconds=app.State.FocusSeconds;string? id=app.State.FocusQuestId;
  var quest=app.State.Quests[0].Children[2];
  try
  {
   app.SetFocusMinutes(25);app.ResetTimer(false);app.ToggleTimer(quest.Id);pump(80);
   int remaining=app.State.FocusSeconds;
   Check(app.SetFocusMinutes(45)&&app.Running&&app.State.FocusSeconds==remaining&&app.FocusDuration(quest.Id)==1500,"Changing default duration preserves active deadline and thread duration");
   var stored=app.Store.Load();Check(stored.FocusMinutes==45&&stored.FocusSessionMinutes==25,"Current session duration persists separately from default");
   app.ToggleTimer(quest.Id);app.State.FocusSeconds=1490;
   Check(app.SetFocusMinutes(60)&&!app.Running&&app.State.FocusSeconds==1490&&app.FocusDuration(quest.Id)==1500,"Changing default duration preserves paused session");
   app.ResetTimer(false);Check(app.State.FocusSeconds==3600&&app.FocusDuration(quest.Id)==3600,"Reset starts with the new duration");
   using(var locked=File.Open(app.Store.FilePath+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
    Check(!app.SetFocusMinutes(45,false)&&app.State.FocusMinutes==60&&app.State.FocusSeconds==3600,"Failed duration save rolls back default and session");
   Check(app.SetFocusMinutes(180)&&app.TimerLabel(quest.Id)=="180:00","Maximum duration applies to an idle timer");
  }
  finally
  {
   if(app.Running)app.ToggleTimer(quest.Id);
   app.State.FocusMinutes=minutes;app.State.FocusSessionMinutes=session;app.State.FocusSeconds=seconds;app.State.FocusQuestId=id;app.Commit();
  }
 }
}
