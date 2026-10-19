using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace SilkJournal;

public sealed class OverlayWindow : Window
{
 readonly App app;
 readonly StackPanel body=new();
 readonly Grid shell=new();
 TextBlock timer=UI.Text("",18,null,true);
 bool ready,fitPending;
 readonly DispatcherTimer boundsSaveTimer=new(){Interval=TimeSpan.FromMilliseconds(300)};
 public OverlayWindow(App app)
 {
  this.app=app;Title=L.T("Закреплённый квест · Журнал странника");Topmost=true;ShowInTaskbar=false;ShowActivated=false;
  WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResizeWithGrip;MinWidth=280;MinHeight=110;
  Width=Math.Clamp(app.State.OverlayWidth,280,750);Height=Math.Clamp(app.State.OverlayHeight,135,800);
  WindowStartupLocation=WindowStartupLocation.Manual;
  WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(7),CornerRadius=new CornerRadius(6),GlassFrameThickness=new Thickness(0)});
  var frame=new Border{BorderBrush=UI.B("#8A7770"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Background=UI.Ink,Padding=new Thickness(1)};Content=frame;var layers=new Grid();frame.Child=layers;layers.Children.Add(new JournalSurface());layers.Children.Add(new NeedleCorner{Height=90,Opacity=.35,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false});layers.Children.Add(shell);layers.Children.Add(new PatinaLayer(app));WindowFrame.Attach(this,layers,6);Icon=BrandAssets.WindowIcon();
  shell.RowDefinitions.Add(new RowDefinition{Height=new GridLength(26)});shell.RowDefinitions.Add(new RowDefinition());
  var bar=new Grid{Background=new LinearGradientBrush(UI.Color("#3C272D"),UI.Color("#151C25"),0)};bar.ColumnDefinitions.Add(new ColumnDefinition());bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  bar.MouseLeftButtonDown+=(_,e)=>{if(e.LeftButton==MouseButtonState.Pressed){try{DragMove();}catch(InvalidOperationException){}SaveBounds();}};
  var buttons=new StackPanel{Orientation=Orientation.Horizontal};
  void Small(string s,string tip,Action a){var b=UI.Button(s,a);b.ToolTip=tip;b.Width=26;b.FontSize=14;b.Margin=new Thickness(0);b.Padding=new Thickness(2);b.Background=Brushes.Transparent;b.BorderThickness=new Thickness(0);buttons.Children.Add(b);}
  Small("↗",L.T("Открыть журнал"),app.ShowJournal);
  Small("≡",L.T("Компактный режим"),()=>{app.State.OverlayCompact=!app.State.OverlayCompact;ApplyAppearance();app.Commit(false);});
  Small("×",L.T("Скрыть висящий квест"),app.HideOverlay);Grid.SetColumn(buttons,1);bar.Children.Add(buttons);shell.Children.Add(bar);
  var scroll=UI.Scroll(body,new Thickness(17,9,17,9));Grid.SetRow(scroll,1);shell.Children.Add(scroll);
  Loaded+=(_,_)=>{RestorePosition();ready=true;ApplyAppearance();SaveBounds();};
  LocationChanged+=(_,_)=>SaveBounds();SizeChanged+=(_,e)=>{SaveBounds();if(e.WidthChanged)FitToContent();};
  boundsSaveTimer.Tick+=(_,_)=>{boundsSaveTimer.Stop();app.Commit(false);};
  Closed+=(_,_)=>{boundsSaveTimer.Stop();app.Changed-=Refresh;app.Tick-=TimerTick;app.OverlayClosed(this);};
  app.Changed+=Refresh;app.Tick+=TimerTick;Refresh();
 }
 public void ApplyAppearance()
 {
  Title=L.T("Закреплённый квест · Журнал странника");
  Opacity=app.State.OverlayOpacity;
  MinHeight=app.State.OverlayCompact?110:150;
  Refresh();
 }
 public void Refresh()
 {
  var q=app.State.Find(app.State.PinnedId);body.Children.Clear();if(q==null){body.Children.Add(UI.Text(L.T("Выбери квест в журнале."),18));return;}
  var title=UI.Text(q.Title,app.State.OverlayFontSize,null,true);title.Margin=new Thickness(0,0,0,8);body.Children.Add(title);
  if(!app.State.OverlayCompact)
  {
   if(!string.IsNullOrEmpty(q.Notes)){var note=UI.Text(q.Notes,12,UI.Muted);note.MaxHeight=50;note.TextTrimming=TextTrimming.CharacterEllipsis;body.Children.Add(note);}
   if(q.Children.Count>0)
   {
    body.Children.Add(UI.Progress(q.Progress));
    var next=q.Children.FirstOrDefault(c=>!c.Done);if(next!=null){var step=UI.Text(L.T("Следующий шаг: ")+next.Title,12,UI.Muted);step.Margin=new Thickness(0,0,0,9);body.Children.Add(step);}
   }
  }
  var actions=new Grid();actions.ColumnDefinitions.Add(new ColumnDefinition());actions.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var done=UI.Button(q.Done?L.T("↶  Вернуть"):L.T("✓  Завершить"),()=>{app.Complete(q,!q.Done);UI.Fade(body,app.State.Motion,.4);},!q.Done);done.Margin=new Thickness(0,4,9,0);done.Padding=new Thickness(10,7,10,7);actions.Children.Add(done);
  var focus=UI.Button(app.TimerLabel(q.Id),()=>app.ToggleTimer(q.Id));focus.ToolTip=L.T("Начать / приостановить фокус");focus.IsEnabled=!q.Done;focus.Margin=new Thickness(0,4,0,0);focus.Padding=new Thickness(10,7,10,7);Grid.SetColumn(focus,1);actions.Children.Add(focus);body.Children.Add(actions);
  timer=UI.Text("",18,null,true);focus.Content=timer;timer.Text=app.TimerLabel(q.Id);TimerTick();FitToContent();
 }
 void FitToContent()
 {
  if(!ready||fitPending)return;fitPending=true;
  Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{
   fitPending=false;if(!IsLoaded)return;
   body.Measure(new Size(Math.Max(1,Width-38),double.PositiveInfinity));
   double height=Math.Clamp(Math.Ceiling(body.DesiredSize.Height+48),MinHeight,Math.Max(MinHeight,MonitorArea().Height-40));
   if(Math.Abs(Height-height)>.5)Height=height;
   ClampToMonitor();SaveBounds();
  }));
 }
 void TimerTick(){timer.Text=(app.Running&&app.State.FocusQuestId==app.State.PinnedId?"Ⅱ  ":"▶  ")+app.TimerLabel(app.State.PinnedId);}
 void RestorePosition()
 {
  if(app.State.OverlayLeft==-1&&app.State.OverlayTop==-1){Snap("tr");return;}
  Left=app.State.OverlayLeft;Top=app.State.OverlayTop;
  // Native nearest-monitor lookup handles disconnected and repositioned displays.
  ClampToMonitor();
 }
 Rect MonitorArea()
 {
  var handle=new WindowInteropHelper(this).Handle;
  var screen=System.Windows.Forms.Screen.FromHandle(handle);
  var a=screen.WorkingArea;var source=PresentationSource.FromVisual(this);
  var m=source?.CompositionTarget?.TransformFromDevice??Matrix.Identity;
  var start=m.Transform(new Point(a.Left,a.Top));var end=m.Transform(new Point(a.Right,a.Bottom));return new Rect(start,end);
 }
 void ClampToMonitor()
 {
  var r=MonitorArea();Width=Math.Min(Width,r.Width);Height=Math.Min(Height,r.Height);
  Left=Math.Clamp(Left,r.Left,Math.Max(r.Left,r.Right-Width));Top=Math.Clamp(Top,r.Top,Math.Max(r.Top,r.Bottom-Height));
 }
 public void Snap(string corner)
 {
  var r=MonitorArea();Left=corner.EndsWith("r")?r.Right-Width-20:r.Left+20;Top=corner.StartsWith("b")?r.Bottom-Height-20:r.Top+20;ClampToMonitor();SaveBounds();
 }
 void SaveBounds()
 {
  if(!ready)return;
  if(app.State.OverlayLeft==Left&&app.State.OverlayTop==Top&&app.State.OverlayWidth==Width&&app.State.OverlayHeight==Height)return;
  // Keep memory current so any explicit save or close captures the latest bounds.
  // Disk writes wait until the stream of move/resize events has settled.
  app.State.OverlayLeft=Left;app.State.OverlayTop=Top;app.State.OverlayWidth=Width;app.State.OverlayHeight=Height;
  boundsSaveTimer.Stop();boundsSaveTimer.Start();
 }
}
