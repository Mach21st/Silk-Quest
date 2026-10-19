using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SilkJournal;

public sealed partial class MainWindow
{
 void AddFocusSettings()
 {
  var heading=UI.Text(L.T("Фокус"),23);heading.Margin=new Thickness(0,8,0,14);tree.Children.Add(heading);
  var row=new Grid{Margin=new Thickness(0,0,0,12)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var label=UI.Text(L.T("Длительность фокуса"),16);label.Margin=new Thickness(0,0,14,0);row.Children.Add(label);
  var amount=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(amount,1);row.Children.Add(amount);
  var input=new TextBox{Text=app.State.FocusMinutes.ToString(L.Culture),Width=68,MaxLength=3,FontSize=18,Padding=new Thickness(8,6,8,6),TextAlignment=TextAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
  System.Windows.Automation.AutomationProperties.SetName(input,L.T("Длительность фокуса в минутах"));amount.Children.Add(input);
  var unit=UI.Text(L.T("мин"),14,UI.Muted);unit.Margin=new Thickness(8,0,0,0);amount.Children.Add(unit);tree.Children.Add(row);
  var slider=new Slider{Minimum=1,Maximum=180,Value=app.State.FocusMinutes,TickFrequency=1,SmallChange=1,LargeChange=5,IsSnapToTickEnabled=true,Margin=new Thickness(0,0,0,8)};
  System.Windows.Automation.AutomationProperties.SetName(slider,L.T("Длительность фокуса"));tree.Children.Add(slider);
  bool updating=false;
  void Set(int value)
  {
   if(updating)return;updating=true;
   try{app.SetFocusMinutes(value);slider.Value=app.State.FocusMinutes;input.Text=app.State.FocusMinutes.ToString(L.Culture);}
   finally{updating=false;}
  }
  slider.ValueChanged+=(_,e)=>Set((int)e.NewValue);
  input.PreviewTextInput+=(_,e)=>e.Handled=e.Text.Any(c=>c<'0'||c>'9');
  DataObject.AddPastingHandler(input,(_,e)=>{if(!e.DataObject.GetDataPresent(DataFormats.UnicodeText)||e.DataObject.GetData(DataFormats.UnicodeText) is not string value||!int.TryParse(value,out int minutes)||minutes<1||minutes>180)e.CancelCommand();});
  void ApplyInput(){if(int.TryParse(input.Text,out int value))Set(Math.Clamp(value,1,180));else input.Text=app.State.FocusMinutes.ToString(L.Culture);}
  input.LostKeyboardFocus+=(_,_)=>ApplyInput();input.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter){ApplyInput();e.Handled=true;}};
  var presets=new WrapPanel{Margin=new Thickness(0,0,0,10)};
  foreach(int minutes in new[]{15,25,45,60}){var button=UI.Button(L.F("{0:0} мин",minutes),()=>Set(minutes));button.FontSize=14;button.Padding=new Thickness(12,6,12,6);button.Margin=new Thickness(0,0,8,6);presets.Children.Add(button);}tree.Children.Add(presets);
  tree.Children.Add(UI.SettingsNote(L.T("От 1 до 180 минут. Текущий и приостановленный сеансы сохраняют своё время; новая длительность действует со следующего фокуса.")));
 }
}
