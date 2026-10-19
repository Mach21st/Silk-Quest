using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace SilkJournal;

static class L
{
 public static string Language {get;set;}="ru";
 public static bool English=>Language=="en";
 public static CultureInfo Culture=>CultureInfo.GetCultureInfo(English?"en-US":"ru-RU");
 static readonly Dictionary<string,string> translations=Load();
 static Dictionary<string,string> Load()
 {
  using var stream=typeof(L).Assembly.GetManifestResourceStream("SilkJournal.Assets.en.json")!;
  return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
 }
 public static string Beads(long amount,bool genitive=false)
 {
  long last=Math.Abs(amount%100),unit=last%10;
  string noun=English?"rosaries":last>=11&&last<=14?"бусин":unit==1?(genitive?"бусины":"бусина"):unit>=2&&unit<=4?(genitive?"бусин":"бусины"):"бусин";
  return amount.ToString("N0",Culture)+" "+noun;
 }
 public static string T(string text)=>English&&translations.TryGetValue(text,out var value)?value:text;
 public static string F(string format,params object[] args)=>string.Format(Culture,T(format),args);
}

public sealed partial class App
{
 public void SetLanguage(string language)
 {
  if(language!="ru"&&language!="en"||State.Language==language)return;
  Journal.FlushEditors();string before=State.Language;State.Language=language;L.Language=language;
  if(!Commit(false)){State.Language=before;L.Language=before;}
  Journal.ApplyLanguage();Overlay?.ApplyAppearance();UpdateTrayLanguage();RefreshViews();
 }
 void UpdateTrayLanguage()
 {
  if(tray==null)return;
  tray.Text=L.T("Silk Quest · Журнал странника");
  var items=tray.ContextMenuStrip?.Items;if(items==null)return;
  items[0].Text=L.T("Открыть журнал");items[1].Text=L.T("Показать / скрыть висящий квест");items[2].Text=L.T("Выйти");
 }
}

public sealed partial class MainWindow
{
 System.Windows.Controls.TextBlock? brandLabel;
 System.Windows.Controls.StackPanel? purseControl;
 public void ApplyLanguage()
 {
  Title=L.T("Журнал странника · Silk Journal");
  if(brandLabel!=null)brandLabel.Text=L.T("Журнал странника");
  if(purseControl!=null)purseControl.ToolTip=L.T("Бусы · базовые награды в настройках; личная награда — в редакторе квеста.");
  undoButton.Content=L.T("Отменить");toastTimer.Stop();toast.Text=DefaultStatus();
 }
 void AddLanguageSettings()
 {
  tree.Children.Add(UI.Text(L.English?"Language":"Язык интерфейса",23));
  var choices=new System.Windows.Controls.WrapPanel{Margin=new System.Windows.Thickness(0,12,0,0)};
  foreach(var choice in new[]{("ru","Русский"),("en","English")})
  {
   var button=UI.Button(choice.Item2,()=>app.SetLanguage(choice.Item1),app.State.Language==choice.Item1);
   button.Margin=new System.Windows.Thickness(0,0,8,0);choices.Children.Add(button);
  }
  tree.Children.Add(choices);tree.Children.Add(UI.Rule());
 }
}
