using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace SilkJournal;

public sealed partial class MainWindow : Window
{
 readonly App app;
 readonly StackPanel nav=new(), tree=new(), details=new();
 static readonly string[] Pages={"Сегодня","Все квесты","Путешествие","Завершённые","Лавка","Настройки"};
 readonly Grid navHost=new();
 readonly TranslateTransform navOffset=new();
 readonly Border navHighlight=new(){IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,6,0),CornerRadius=new CornerRadius(4),BorderThickness=new Thickness(3,0,0,0),BorderBrush=UI.Red,Background=new LinearGradientBrush(UI.Color("#34262B"),UI.Color("#151B23"),0),Opacity=0};
 bool highlightReady;
 readonly TextBlock pageTitle=UI.Text("Сегодня",42,null,true), subTitle=UI.Text("",20,UI.Muted,true), daily=UI.Text("",19,UI.Paper,true), toast=UI.Text("",11,UI.Muted);
 readonly SilkCompass compass=new();
 readonly RosaryCounter rosaries=new();
 readonly TextBlock rewardGain=UI.Text("",15,UI.Red);
 readonly Button undoButton=UI.Button(L.T("Отменить"),()=>App.CurrentApp.Undo());
 readonly Viewbox compassHost=new(){Width=148,Height=148};
 readonly Grid contentArea=new();
 TextBox? titleEditor, notesEditor;
 string? editingId;
 TextBlock? timerText;
 Button? timerButton;
 SpoolIcon? focusSpool;
 string page="Сегодня";
 bool rendering,editMode;
 readonly HashSet<string> expanded=new();
 DispatcherTimer toastTimer=new();
 public MainWindow(App app)
 {
  this.app=app;ReloadViewPreferences(); Title=L.T("Журнал странника · Silk Journal"); Width=1400; Height=900; MinWidth=Math.Min(1100,Math.Max(700,SystemParameters.WorkArea.Width-24)); MinHeight=Math.Min(640,Math.Max(440,SystemParameters.WorkArea.Height-24));
  UseLayoutRounding=true;SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Ideal);TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);
  Width=Math.Min(Width,SystemParameters.WorkArea.Width-40); Height=Math.Min(Height,SystemParameters.WorkArea.Height-40);
  WindowStartupLocation=WindowStartupLocation.CenterScreen; WindowStyle=WindowStyle.None;
  WindowChrome.SetWindowChrome(this,new WindowChrome { CaptionHeight=0, ResizeBorderThickness=new Thickness(6), CornerRadius=new CornerRadius(6), GlassFrameThickness=new Thickness(0) });
  var outer=new Border { BorderBrush=UI.B("#A39B8E"), BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(6), ClipToBounds=true };
  Content=outer;var layers=new Grid();outer.Child=layers;layers.Children.Add(new JournalSurface());
  var shell=new Grid(); shell.RowDefinitions.Add(new RowDefinition { Height=new GridLength(47) }); shell.RowDefinitions.Add(new RowDefinition()); shell.RowDefinitions.Add(new RowDefinition {Height=new GridLength(27)}); layers.Children.Add(shell);
  var chrome=new Grid { Background=UI.B("#30111A23") }; chrome.ColumnDefinitions.Add(new ColumnDefinition()); chrome.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  chrome.MouseLeftButtonDown+=(_,e)=> { if(e.ClickCount==2) WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized; else if(e.LeftButton==MouseButtonState.Pressed) DragMove(); };
  var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(24,0,0,0)};brand.Children.Add(new JournalIcon("flower",false,25));var brandText=brandLabel=UI.Text(L.T("Журнал странника"),17,null,true);brandText.Margin=new Thickness(14,0,0,0);brand.Children.Add(brandText);chrome.Children.Add(brand);
  var controls=new StackPanel{Orientation=Orientation.Horizontal};
  foreach(var item in new (string,Action)[]{("—",()=>WindowState=WindowState.Minimized),("□",()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized),("×",()=>Close())})
  { var b=UI.Button(item.Item1,item.Item2); b.Width=45;b.Margin=new Thickness(0);b.BorderThickness=new Thickness(0);b.Background=Brushes.Transparent; controls.Children.Add(b); }
  Grid.SetColumn(controls,1);chrome.Children.Add(controls);shell.Children.Add(chrome);
  var topRule=new Border{Height=.7,Background=UI.B("#686861"),VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};Grid.SetColumnSpan(topRule,2);chrome.Children.Add(topRule);
  var body=new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(19,GridUnitType.Star),MinWidth=222,MaxWidth=290});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(49,GridUnitType.Star),MinWidth=410});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32,GridUnitType.Star),MinWidth=350,MaxWidth=540}); Grid.SetRow(body,1);shell.Children.Add(body);
  var sideLayer=new Grid();sideLayer.Children.Add(new JournalSurface(true));body.Children.Add(sideLayer);
  var side=new Grid{Margin=new Thickness(0,30,0,18)};side.RowDefinitions.Add(new RowDefinition());side.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});navHighlight.RenderTransform=navOffset;navHost.Children.Add(navHighlight);navHost.Children.Add(nav);side.Children.Add(UI.Scroll(navHost));sideLayer.Children.Add(side);navHost.SizeChanged+=(_,_)=>MoveHighlight(false);
  var bottom=new StackPanel{Margin=new Thickness(27,0,27,0)};bottom.Children.Add(UI.Rule(8));compassHost.Child=compass;compassHost.HorizontalAlignment=HorizontalAlignment.Center;bottom.Children.Add(compassHost);daily.TextAlignment=TextAlignment.Center;daily.Margin=new Thickness(0,6,0,12);bottom.Children.Add(daily);bottom.Children.Add(UI.Rule(7));Grid.SetRow(bottom,1);side.Children.Add(bottom);
  var purse=purseControl=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,7,0,0),ToolTip=L.T("Бусы · базовые награды в настройках; личная награда — в редакторе квеста.")};purse.Children.Add(new RosaryIcon());rosaries.Margin=new Thickness(9,0,0,0);var counterHost=new Grid();counterHost.Children.Add(rosaries);rewardGain.Opacity=0;rewardGain.IsHitTestVisible=false;rewardGain.VerticalAlignment=VerticalAlignment.Top;rewardGain.Margin=new Thickness(9,-8,0,0);counterHost.Children.Add(rewardGain);purse.Children.Add(counterHost);bottom.Children.Add(purse);
  sideLayer.Children.Add(new PanelSpine{HorizontalAlignment=HorizontalAlignment.Right,Width=15,Margin=new Thickness(0,13,0,0)});
  var area=shopMainArea=new Grid{Margin=new Thickness(31,30,29,4)};area.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});area.RowDefinitions.Add(new RowDefinition()); Grid.SetColumn(area,1);body.Children.Add(area);
  var header=new StackPanel{Margin=new Thickness(0,4,0,6)};header.Children.Add(pageTitle);subTitle.Margin=new Thickness(0,8,0,0);header.Children.Add(subTitle);header.Children.Add(UI.Rule(19));area.Children.Add(header);
  tasksScroller=UI.Scroll(tree);Grid.SetRow(tasksScroller,1);area.Children.Add(tasksScroller);
  var detailGrid=shopDetailArea=new Grid();Grid.SetColumn(detailGrid,2);body.Children.Add(detailGrid);
  detailGrid.Children.Add(new PanelSpine{HorizontalAlignment=HorizontalAlignment.Left,Width=14,Margin=new Thickness(0,13,0,0)});
  detailGrid.Children.Add(new NeedleCorner{Width=330,Height=140,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Opacity=.95});
  detailScroller=UI.Scroll(details,new Thickness(35,42,30,44));detailGrid.Children.Add(detailScroller);
  var footer=new Grid();footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Grid.SetRow(footer,2);shell.Children.Add(footer);toast.Margin=new Thickness(25,0,12,0);toast.TextWrapping=TextWrapping.NoWrap;toast.TextTrimming=TextTrimming.CharacterEllipsis;footer.Children.Add(toast);undoButton.FontSize=12;undoButton.Padding=new Thickness(10,0,10,0);undoButton.Margin=new Thickness(0,1,20,1);undoButton.MinHeight=0;undoButton.Visibility=Visibility.Collapsed;Grid.SetColumn(undoButton,1);footer.Children.Add(undoButton);
  layers.Children.Add(new PatinaLayer(app));WindowFrame.Attach(this,layers,6);
  toastTimer.Interval=TimeSpan.FromSeconds(6);toastTimer.Tick+=(_,_)=>{toast.Text=DefaultStatus();toastTimer.Stop();};
  app.Changed+=Render;app.Tick+=UpdateTimer;
  app.UndoChanged+=UpdateUndoButton;app.RewardEarned+=ShowRewardGain;InitViewMemory();SizeChanged+=(_,_)=>FitCompactLayout(body,side);Loaded+=(_,_)=>FitCompactLayout(body,side);
  Closing+=(_,e)=> { FlushEditors();if(!app.Exiting){ e.Cancel=true; if(app.Commit(false))Hide(); } };
  PreviewKeyDown+=HandleKeys;
  Render();
  var phoneStatusTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};phoneStatusTimer.Tick+=(_,_)=>{UpdateUndoButton();UpdatePhoneStatus();};phoneStatusTimer.Start();Closed+=(_,_)=>{phoneStatusTimer.Stop();viewSaveTimer.Stop();};
 }
 string DefaultStatus()=>app.SaveHealthy?L.T("Локальное сохранение   ·   Ctrl+N — новый квест   ·   Журнал остаётся в трее"):L.T("⚠ Изменения пока не сохранены на диск");
 public void Toast(string message,bool undo=false){toast.Text=message;toastTimer.Stop();toastTimer.Interval=TimeSpan.FromSeconds(undo?10:6);toastTimer.Start();UI.Fade(toast,app.State.Motion);UpdateUndoButton();}
 public void FlushEditors()
 {
  var q=app.State.Find(editingId);if(q==null||titleEditor==null||notesEditor==null)return;
  if(!string.IsNullOrWhiteSpace(titleEditor.Text))q.Title=titleEditor.Text.Trim();q.Notes=notesEditor.Text;
 }
 void Navigate(string value){FlushEditors();CaptureViewState();page=value;app.Commit();}
 internal string CurrentPage=>page;
 internal double HighlightPosition=>navOffset.Y;
 internal void NavigateRelative(int direction)=>Navigate(Pages[(Array.IndexOf(Pages,page)+direction+Pages.Length)%Pages.Length]);
 void HandleKeys(object sender,KeyEventArgs e)
 {
  if(e.Key==Key.N&&Keyboard.Modifiers==ModifierKeys.Control){NewQuest();e.Handled=true;return;}
  if(e.Key==Key.S&&Keyboard.Modifiers==ModifierKeys.Control){FlushEditors();app.Commit();Toast(L.T("Изменения сохранены."));e.Handled=true;return;}
  if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control&&Keyboard.FocusedElement is not TextBoxBase){app.Undo();e.Handled=true;return;}
  if(Keyboard.Modifiers!=ModifierKeys.None||e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down))return;
  DependencyObject? focused=Keyboard.FocusedElement as DependencyObject;
  while(focused!=null)
  {
   if(focused is TextBoxBase or Slider or ComboBox or ComboBoxItem or MenuItem)return;
   focused=focused is Visual?VisualTreeHelper.GetParent(focused):LogicalTreeHelper.GetParent(focused);
  }
  NavigateRelative(e.Key is Key.Left or Key.Up?-1:1);e.Handled=true;
 }
 void MoveHighlight(bool animate)
 {
  int index=Array.IndexOf(Pages,page);if(index<0||index>=nav.Children.Count)return;
  var button=(FrameworkElement)nav.Children[index];if(!button.IsArrangeValid||button.ActualHeight<1)return;
  double target=button.TranslatePoint(new Point(),navHost).Y;
  double from=navOffset.Y,oldHeight=navHighlight.ActualHeight;
  navOffset.BeginAnimation(TranslateTransform.YProperty,null);navOffset.Y=target;
  navHighlight.BeginAnimation(HeightProperty,null);navHighlight.Height=button.ActualHeight;navHighlight.Opacity=1;
  if(animate&&highlightReady&&app.State.Motion&&Math.Abs(target-from)>.1)
  {
   var ease=new CubicEase{EasingMode=EasingMode.EaseInOut};
   navOffset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(from,target,TimeSpan.FromMilliseconds(260)){EasingFunction=ease});
   navHighlight.BeginAnimation(HeightProperty,new DoubleAnimation(oldHeight,button.ActualHeight,TimeSpan.FromMilliseconds(260)){EasingFunction=ease});
  }
  if(animate&&Math.Abs(target-from)>.1)button.BringIntoView();
  highlightReady=true;
 }
 public void ShowSettings()=>Navigate("Настройки");
 internal bool FocusSpoolRunning=>focusSpool?.IsSpinning??false;
 internal double FocusSpoolAngle=>focusSpool?.Angle??0;
 internal double FocusSpoolWinding=>focusSpool?.Winding??0;
 internal double FocusSpoolLooseThread=>focusSpool?.LooseThread??0;
 void Select(Quest q){FlushEditors();editMode=false;app.Select(q.Id);}
 void NewQuest(Quest? parent=null)
 {
  FlushEditors();var q=new Quest{Title=parent==null?L.T("Новый квест"):L.T("Новый шаг")};
  if(parent==null)app.State.Quests.Add(q);else { parent.Children.Add(q);parent.Done=false; }
  if(page=="Завершённые"||page=="Настройки"||page=="Путешествие"||page=="Лавка")page="Все квесты";
  editMode=true;app.State.SelectedId=q.Id;app.Commit();titleEditor?.Focus();titleEditor?.SelectAll();
 }
 void Render()
 {
  if(rendering)return;rendering=true;
  try
  {
   // Preserve pending text if a timer or overlay action causes a redraw.
   FlushEditors();BeginViewRender();nav.Children.Clear();
   foreach(var destination in Pages)
   {
    bool active=page==destination;var b=UI.Button(L.T(destination),()=>Navigate(destination));b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.Padding=CompactLayout?new Thickness(16,13,8,13):new Thickness(23,15,12,15);b.Margin=new Thickness(0,0,6,5);b.BorderThickness=new Thickness(3,0,0,0);b.BorderBrush=Brushes.Transparent;
    b.Background=Brushes.Transparent;b.Style=(Style)FindResource("NavigationButton");
    var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(CompactLayout?22:25)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(new JournalIcon(destination,active,CompactLayout?22:25));var t=UI.Text(L.T(destination),CompactLayout?18:21,active?UI.Red:UI.Paper,true);t.Margin=new Thickness(CompactLayout?10:17,0,0,0);t.TextWrapping=TextWrapping.NoWrap;t.TextTrimming=TextTrimming.CharacterEllipsis;b.ToolTip=L.T(destination);Grid.SetColumn(t,1);row.Children.Add(t);b.Content=row;nav.Children.Add(b);
   }
   Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>MoveHighlight(true)));
   rosaries.SetAmount(app.State.Rosaries,app.State.Motion);
   var leaves=app.State.Quests.Where(q=>q.Today).SelectMany(q=>q.Leaves()).ToList();int done=leaves.Count(q=>q.Done);
   compass.SetProgress(leaves.Count==0?0:done/(double)leaves.Count,app.State.Motion);daily.Text=L.F("{0} из {1}\nвыполнено",done,leaves.Count);
   pageTitle.Text=L.T(page);subTitle.Text=page=="Сегодня"?DateTime.Now.ToString("dddd, d MMMM",L.Culture):page=="Настройки"?L.T("Пусть журнал подстроится под тебя."):page=="Путешествие"?L.T("Каждая большая цель — отдельная нить пути."):L.T("Большие цели. Небольшие шаги.");
   tree.Children.Clear();details.Children.Clear();phoneStatusText=null;editingId=null;titleEditor=null;notesEditor=null;timerText=null;timerButton=null;focusSpool=null;
   SetShopLayout(page=="Лавка");
   if(page=="Лавка"){subTitle.Text=L.T("Маленькие вещи, ради которых хочется двигаться дальше.");RenderShop();return;}
   if(page=="Настройки"){RenderSettings();return;}
   if(page=="Путешествие") RenderJourney();else RenderTree();
   RenderDetails();if(!toastTimer.IsEnabled)toast.Text=DefaultStatus();
  }
  finally{rendering=false;FinishViewRender();}
 }
 void RenderTree()
 {
  var roots=app.State.Quests.Where(q=>page!="Сегодня"||q.Today).Where(q=>page!="Завершённые"||q.Done).ToList();
  if(roots.Count==0){tree.Children.Add(UI.Text(page=="Завершённые"?L.T("Здесь появятся завершённые квесты."):L.T("Новая страница твоего пути."),23,UI.Paper,true));var s=UI.Text(L.T("Добавь квест кнопкой сверху. Большую цель можно разделить на небольшие шаги."),14,UI.Muted);s.Margin=new Thickness(0,15,0,0);tree.Children.Add(s);return;}
  var projects=roots.Where(q=>q.Children.Count>0).ToList();var singles=roots.Where(q=>q.Children.Count==0).ToList();
  foreach(var q in projects)
  {
   AddProjectHeader(q);
   if(!app.State.CollapsedQuestIds.Contains(q.Id))tree.Children.Add(BuildThreadGroup(q.Children,1));
   tree.Children.Add(UI.Rule(12));
  }
  if(singles.Count>0)
  {
   var heading=UI.Text(projects.Count>0?L.T("Отдельные квесты"):L.T("Твои квесты"),20,UI.Paper,true);heading.Margin=new Thickness(0,0,0,8);tree.Children.Add(heading);
   foreach(var q in singles){tree.Children.Add(AddRow(q,0));tree.Children.Add(new Border{Height=.6,Background=UI.B("#34404A")});}
  }
  var add=UI.Button(L.T("＋  Добавить квест"),()=>NewQuest());add.Background=Brushes.Transparent;add.BorderThickness=new Thickness(0);add.Foreground=UI.B("#BFB5A5");add.Margin=new Thickness(30,8,30,5);tree.Children.Add(add);
 }
 void AddProjectHeader(Quest q)
 {
  var wrap=new Grid{Margin=new Thickness(0,0,0,12)};wrap.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(41)});wrap.ColumnDefinitions.Add(new ColumnDefinition());wrap.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(CompactLayout?90:120)});
  var seal=UI.Button("",()=>{FlushEditors();app.Complete(q,!q.Done);});seal.Content=q.Done?(object)new QuestSeal(true):new JournalIcon("project",false,31);seal.Background=Brushes.Transparent;seal.BorderThickness=new Thickness(0);seal.Padding=new Thickness(0);seal.Margin=new Thickness(0);seal.VerticalAlignment=VerticalAlignment.Top;seal.ToolTip=L.T("Завершить / вернуть весь квест");wrap.Children.Add(seal);
  var titleGroup=new StackPanel();var title=UI.Button(q.Title,()=>{if(!app.State.CollapsedQuestIds.Add(q.Id))app.State.CollapsedQuestIds.Remove(q.Id);Select(q);});var titleText=UI.Text(q.Title,CompactLayout?23:27,UI.Paper,true);titleText.LineHeight=titleText.FontSize*1.2;titleText.MaxHeight=titleText.LineHeight*3;titleText.TextTrimming=TextTrimming.CharacterEllipsis;title.ToolTip=q.Title;title.Content=titleText;title.Background=Brushes.Transparent;title.BorderThickness=new Thickness(0);title.HorizontalContentAlignment=HorizontalAlignment.Stretch;title.Padding=new Thickness(0);title.Margin=new Thickness(0,0,0,10);titleGroup.Children.Add(title);
  var tag=new Border{BorderBrush=UI.B("#4D5355"),BorderThickness=new Thickness(.7),CornerRadius=new CornerRadius(3),Padding=new Thickness(9,3,9,3),HorizontalAlignment=HorizontalAlignment.Left};var text=UI.Text(KindLabel(app.State.KindOf(q)).ToUpperInvariant()+L.T(" КВЕСТ"),10,UI.Muted);tag.Child=text;titleGroup.Children.Add(tag);Grid.SetColumn(titleGroup,1);wrap.Children.Add(titleGroup);
  var stats=new StackPanel{VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(9,0,0,0)};stats.Children.Add(UI.Text($"{q.Children.Count(c=>c.Done)} / {q.Children.Count}",15,UI.Paper,true));stats.Children.Add(UI.Progress(q.Children.Count(c=>c.Done)/(double)q.Children.Count));Grid.SetColumn(stats,2);wrap.Children.Add(stats);tree.Children.Add(wrap);
 }
 FrameworkElement BuildThreadGroup(IReadOnlyList<Quest> quests,int depth)
 {
  var group=new Grid{Margin=new Thickness(depth>1?26:0,0,0,0)};var rows=new StackPanel();var silk=new SilkThread();group.Children.Add(silk);group.Children.Add(rows);
  foreach(var q in quests){var row=AddRow(q,depth);rows.Children.Add(row);silk.Watch(row,q.Done);if(expanded.Contains(q.Id)&&q.Children.Count>0)rows.Children.Add(BuildThreadGroup(q.Children,depth+1));}
  group.SizeChanged+=(_,_)=>silk.InvalidateVisual();return group;
 }
 FrameworkElement AddRow(Quest q,int depth)
 {
  bool selected=q.Id==app.State.SelectedId;
  var line=new Grid();line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(depth>0?40:0)});line.ColumnDefinitions.Add(new ColumnDefinition());
  var frame=new Border{MinHeight=64,CornerRadius=new CornerRadius(7),Margin=new Thickness(0,2,0,2),BorderThickness=new Thickness(selected?.8:0),BorderBrush=UI.B("#A64A48"),Background=selected?new LinearGradientBrush(UI.Color("#43292D"),UI.Color("#251E25"),0):Brushes.Transparent};Grid.SetColumn(frame,1);line.Children.Add(frame);
  var row=new Grid{Margin=new Thickness(3,0,8,0)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(37)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});frame.Child=row;
  var check=UI.Button(q.Done?L.T("Вернуть в работу"):L.T("Завершить квест"),()=>{FlushEditors();app.Complete(q,!q.Done);});check.Content=new QuestSeal(q.Done,selected);check.Padding=new Thickness(0);check.Margin=new Thickness(0);check.Background=Brushes.Transparent;check.BorderThickness=new Thickness(0);check.ToolTip=q.Done?L.T("Вернуть в работу"):L.T("Завершить квест");row.Children.Add(check);
  var label=UI.Text(q.Title,CompactLayout?18:21,q.Done?UI.B("#9B9A95"):UI.Paper,true);label.LineHeight=label.FontSize*1.25;label.MaxHeight=label.LineHeight*2;label.TextTrimming=TextTrimming.CharacterEllipsis;label.ToolTip=q.Title;if(q.Done)label.TextDecorations=TextDecorations.Strikethrough;
  var select=new Button{Content=label,Padding=new Thickness(9,12,5,12),Margin=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,BorderThickness=new Thickness(0),Background=Brushes.Transparent};select.Click+=(_,_)=>Select(q);Grid.SetColumn(select,1);row.Children.Add(select);
  var tools=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
  if(selected&&!CompactLayout){var badge=new Border{BorderBrush=UI.B("#8D4746"),BorderThickness=new Thickness(.7),CornerRadius=new CornerRadius(3),Padding=new Thickness(8,4,8,4),Margin=new Thickness(5,0,5,0),Child=UI.Text(L.T("В фокусе"),12,UI.Red,true)};tools.Children.Add(badge);}
  if(q.Children.Count>0){var expand=UI.Button(expanded.Contains(q.Id)?"⌄":"›",()=>{if(!expanded.Add(q.Id))expanded.Remove(q.Id);app.Commit();});expand.ToolTip=L.T("Раскрыть / свернуть шаги");expand.Margin=new Thickness(0);expand.Padding=new Thickness(5);expand.BorderThickness=new Thickness(0);expand.Background=Brushes.Transparent;tools.Children.Add(expand);}
  var more=UI.Button("···",()=>OpenQuestMenu(q));System.Windows.Automation.AutomationProperties.SetName(more,L.T("Действия: ")+q.Title);more.ToolTip=L.T("Действия с квестом");more.Foreground=UI.Muted;more.Background=Brushes.Transparent;more.BorderThickness=new Thickness(0);more.Margin=new Thickness(0);more.Padding=new Thickness(5);more.FontSize=18;
  // Reserve the space: revealing actions must never shift the task text.
  more.Opacity=0;void RevealActions(){bool show=frame.IsMouseOver||frame.IsKeyboardFocusWithin;double from=more.Opacity;more.BeginAnimation(UIElement.OpacityProperty,null);double value=show?1:0;more.Opacity=value;if(app.State.Motion)more.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(from,value,TimeSpan.FromMilliseconds(140)));more.IsHitTestVisible=show;}
  more.IsHitTestVisible=false;frame.MouseEnter+=(_,_)=>RevealActions();frame.MouseLeave+=(_,_)=>RevealActions();frame.IsKeyboardFocusWithinChanged+=(_,_)=>RevealActions();
  tools.Children.Add(more);Grid.SetColumn(tools,2);row.Children.Add(tools);if(selected)UI.Fade(frame,app.State.Motion,.35);return line;
 }
 void OpenQuestMenu(Quest q)
 {
  var menu=new ContextMenu();
  void Item(string title,Action action){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();menu.Items.Add(item);}
  Item(L.T("Редактировать"),()=>{FlushEditors();editMode=true;app.Select(q.Id);});Item(L.T("Поверх всех окон"),()=>app.Pin(q.Id));Item(app.State.PhonePinnedId==q.Id?L.T("Снять с телефона"):L.T("Закрепить на телефоне"),()=>app.PinPhone(app.State.PhonePinnedId==q.Id?null:q.Id));Item(L.T("Добавить подзадачу"),()=>NewQuest(q));Item(L.T("Удалить…"),()=>DeleteQuest(q));menu.IsOpen=true;
 }
 void RenderDetails()
 {
  var q=app.State.Find(app.State.SelectedId);
  if(q==null){details.Children.Add(UI.Text(L.T("Выбери свой следующий шаг"),27,null,true));details.Children.Add(UI.Rule());details.Children.Add(UI.Text(L.T("Нажми на квест, чтобы увидеть детали, включить фокус или закрепить его поверх окон."),14,UI.Muted));return;}
  var heading=UI.Text(q.Done?L.T("✧   З А В Е Р Ш Е Н О"):L.T("●   В  Ф О К У С Е"),11,UI.Red);details.Children.Add(heading);
  var title=UI.Text(q.Title,CompactLayout?28:35,null,true);title.LineHeight=title.FontSize*1.18;title.MaxHeight=title.LineHeight*3;title.TextTrimming=TextTrimming.CharacterEllipsis;title.ToolTip=q.Title;title.Margin=new Thickness(0,20,0,1);details.Children.Add(title);details.Children.Add(UI.Rule(14));
  if(editMode){RenderEditor(q);return;}
  if(!string.IsNullOrEmpty(q.Notes)){var note=UI.Text(q.Notes,21,UI.B("#ADAEAD"),true);note.Margin=new Thickness(0,0,0,17);note.LineHeight=30;details.Children.Add(note);}
  foreach(var child in q.Children)
  {
   var row=new Grid{Margin=new Thickness(0,4,0,4),MinHeight=43};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(36)});row.ColumnDefinitions.Add(new ColumnDefinition());
   var check=UI.Button(child.Done?L.T("Вернуть в работу"):L.T("Завершить шаг"),()=>app.Complete(child,!child.Done));check.Content=new QuestSeal(child.Done);check.Background=Brushes.Transparent;check.BorderThickness=new Thickness(0);check.Padding=new Thickness(0);check.Margin=new Thickness(0);row.Children.Add(check);
   var text=UI.Button(child.Title,()=>Select(child));text.Content=UI.Text(child.Title,20,child.Done?UI.Muted:UI.Paper,true);text.Background=Brushes.Transparent;text.BorderThickness=new Thickness(0);text.HorizontalContentAlignment=HorizontalAlignment.Left;text.Margin=new Thickness(0);text.Padding=new Thickness(9,8,0,8);Grid.SetColumn(text,1);row.Children.Add(text);details.Children.Add(row);
  }
  details.Children.Add(UI.Rule(16));
  int pending=app.State.PendingReward(q);
  var reward=UI.Text(q.Done?L.T("Награда уже учтена"):L.F("✧  За завершение: +{0}",L.Beads(pending)),14,UI.Paper);reward.Margin=new Thickness(0,0,0,12);reward.ToolTip=L.F("Вид: {0}. Личная награда: {1}. Сумма включает неоплаченные шаги и бонусы завершаемых квестов.",KindLabel(app.State.KindOf(q)),app.State.RewardFor(q));details.Children.Add(reward);
  var timerRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,17)};focusSpool=new SpoolIcon();timerRow.Children.Add(focusSpool);timerText=UI.Text(app.TimerLabel(q.Id),22,null,true);timerText.Margin=new Thickness(12,0,0,0);timerRow.Children.Add(timerText);details.Children.Add(timerRow);
  timerButton=UI.Button(L.T("▶  Начать фокус"),()=>app.ToggleTimer(q.Id),true);timerButton.IsEnabled=!q.Done;timerButton.MinHeight=48;details.Children.Add(timerButton);
  var complete=UI.Button(q.Done?L.T("↶   Вернуть в работу"):L.T("✓   Завершить"),()=>app.Complete(q,!q.Done));complete.Background=Brushes.Transparent;complete.BorderBrush=UI.B("#827D71");complete.MinHeight=47;details.Children.Add(complete);
  var tools=new Grid{Margin=new Thickness(0,7,0,0)};tools.ColumnDefinitions.Add(new ColumnDefinition());tools.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var pin=UI.Button(L.T("⌖  Поверх окон"),()=>{app.Pin(q.Id);Toast(L.T("Квест закреплён. Перетащи его за верхнюю полоску."));});pin.Background=Brushes.Transparent;pin.BorderThickness=new Thickness(0);pin.Foreground=UI.Muted;pin.FontSize=13;pin.HorizontalContentAlignment=HorizontalAlignment.Left;pin.Padding=new Thickness(0,8,0,8);tools.Children.Add(pin);
  var edit=UI.Button(L.T("Изменить  ···"),()=>{editMode=true;app.Commit();});edit.FontSize=13;edit.Foreground=UI.Muted;edit.BorderThickness=new Thickness(0);edit.Background=Brushes.Transparent;edit.Padding=new Thickness(0,8,0,8);Grid.SetColumn(edit,1);tools.Children.Add(edit);details.Children.Add(tools);
  UpdateTimer();
 }
 void RenderEditor(Quest q)
 {
  details.Children.Add(UI.Label(L.T("НАЗВАНИЕ")));
  editingId=q.Id;titleEditor=new TextBox{Text=q.Title,FontSize=16,Padding=new Thickness(10,9,10,9),MinHeight=40,Margin=new Thickness(0,7,0,15),MaxLength=300,AcceptsReturn=false};details.Children.Add(titleEditor);
  details.Children.Add(UI.Label(L.T("ЗАМЕТКИ")));notesEditor=new TextBox{Text=q.Notes,FontSize=16,Padding=new Thickness(10),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=110,MaxHeight=200,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,7,0,15)};details.Children.Add(notesEditor);
  details.Children.Add(UI.Label(L.T("ВИД КВЕСТА")));
  var kinds=new WrapPanel{Margin=new Thickness(0,8,0,12)};
  foreach(var kind in new[]{QuestKind.Auto,QuestKind.Main,QuestKind.Side,QuestKind.Step})
  {
   var b=UI.Button(KindLabel(kind),()=>{FlushEditors();q.Kind=kind;app.Commit();},q.Kind==kind);b.FontSize=14;b.Padding=new Thickness(10,6,10,6);b.Margin=new Thickness(0,0,6,6);kinds.Children.Add(b);
  }
  details.Children.Add(kinds);
  var own=new CheckBox{Content=L.T("Своя награда вместо базовой"),IsChecked=q.CustomReward.HasValue};details.Children.Add(own);
  var amount=AddRewardField(details,L.T("Бусы за этот квест"),q.CustomReward??app.State.RewardFor(q),v=>{if(q.CustomReward.HasValue){q.CustomReward=v;app.Commit(false);}},q.CustomReward.HasValue);
  own.Click+=(_,_)=>{FlushEditors();q.CustomReward=own.IsChecked==true?int.Parse(amount.Text):null;app.Commit();};
  var hint=UI.Text(app.State.RewardedQuestIds.Contains(q.Id)?L.T("Награда за этот квест уже учтена. Изменение суммы не начисляет её повторно."):L.T("Авто: квест с шагами — главный; отдельный — побочный; вложенная задача — шаг. Бонус главного квеста выдаётся после всех его шагов."),13,UI.Muted);hint.Margin=new Thickness(0,6,0,14);details.Children.Add(hint);
  details.Children.Add(UI.Button(L.T("Сохранить и вернуться"),()=>{FlushEditors();editMode=false;app.Commit();Toast(L.T("Изменения сохранены."));},true));
  bool root=app.State.Quests.Contains(q);
  if(root){var today=new CheckBox{Content=L.T("Показывать в «Сегодня»"),IsChecked=q.Today};today.Click+=(_,_)=>{FlushEditors();q.Today=today.IsChecked==true;app.Commit();};details.Children.Add(today);}
  details.Children.Add(UI.Button(L.T("＋  Добавить подзадачу"),()=>NewQuest(q)));
  var delete=UI.Button(L.T("Удалить квест…"),()=>DeleteQuest(q));delete.Foreground=UI.Red;details.Children.Add(delete);
 }
 void DeleteQuest(Quest q)
 {
  if(MessageBox.Show(this,L.T("Удалить «")+q.Title+L.T("»")+(q.Children.Count>0?L.T(" и все подзадачи"):"")+"?",L.T("Удаление квеста"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
  app.DeleteQuest(q);
 }
 void RenderJourney()
 {
  if(app.State.Quests.Count==0)tree.Children.Add(UI.Text(L.T("Твой путь начнётся с первого квеста."),23,null,true));
  foreach(var q in app.State.Quests)
  {
   var card=new Border{BorderBrush=UI.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(20),Margin=new Thickness(0,0,0,18),Background=UI.B("#151F29")};var s=new StackPanel();card.Child=s;
   s.Children.Add(UI.Text(q.Done?L.T("✧  ПУТЬ ПРОЙДЕН"):L.T("⌁  ПУТЕШЕСТВИЕ"),10,q.Done?UI.Paper:UI.Red));var t=UI.Text(q.Title,24,null,true);t.Margin=new Thickness(0,12,0,12);s.Children.Add(t);s.Children.Add(UI.Progress(q.Progress));s.Children.Add(UI.Text(L.F("{0:P0}   ·   {1} из {2} шагов",q.Progress,q.Leaves().Count(x=>x.Done),q.Leaves().Count()),13,UI.Muted));
   foreach(var child in q.Children){var step=UI.Text((child.Done?"●":"○")+"   "+child.Title,14,child.Done?UI.Muted:UI.Paper);step.Margin=new Thickness(2,10,0,0);s.Children.Add(step);}
   var open=UI.Button(L.T("Открыть квест →"),()=>Select(q));open.Margin=new Thickness(0,18,0,0);s.Children.Add(open);tree.Children.Add(card);
  }
 }
 void RenderSettings()
 {
  void Section(string text){var t=UI.Text(text,23,null,true);t.Margin=new Thickness(0,8,0,14);tree.Children.Add(t);}
  AddLanguageSettings();AddFocusSettings();tree.Children.Add(UI.Rule());RenderPhoneSettings();tree.Children.Add(UI.Rule());
  Section(L.T("Висящий квест"));tree.Children.Add(UI.SettingsNote(L.T("Выбери квест и нажми «Поверх всех окон». Перетаскивай за верхнюю полоску; тяни за край, чтобы менять размер. Окно помнит своё место.")));
  var show=UI.Button(app.Overlay==null?L.T("Показать выбранный квест"):L.T("Скрыть висящий квест"),()=>{if(app.Overlay==null)app.Pin(app.State.SelectedId);else app.HideOverlay();});show.Margin=new Thickness(0,16,0,12);tree.Children.Add(show);
  AddSlider(L.T("Непрозрачность"),.4,1,app.State.OverlayOpacity,v=>{app.State.OverlayOpacity=v;app.Overlay?.ApplyAppearance();app.Commit(false);},v=>$"{v:P0}");
  AddSlider(L.T("Размер текста"),15,30,app.State.OverlayFontSize,v=>{app.State.OverlayFontSize=v;app.Overlay?.Refresh();app.Commit(false);},v=>$"{v:0} px",true);
  var compact=new CheckBox{Content=L.T("Компактный режим висящего квеста"),IsChecked=app.State.OverlayCompact};compact.Click+=(_,_)=>{app.State.OverlayCompact=compact.IsChecked==true;app.Overlay?.ApplyAppearance();app.Commit(false);};tree.Children.Add(compact);
  var positions=new WrapPanel{Margin=new Thickness(0,12,0,0)};
  foreach(var p in new[]{(L.T("↖  Слева сверху"),"tl"),(L.T("↗  Справа сверху"),"tr"),(L.T("↙  Слева снизу"),"bl"),(L.T("↘  Справа снизу"),"br")}){var b=UI.Button(p.Item1,()=>{if(app.Overlay==null)app.Pin(app.State.SelectedId);app.Overlay?.Snap(p.Item2);});b.Margin=new Thickness(0,0,7,8);positions.Children.Add(b);}tree.Children.Add(positions);
  tree.Children.Add(UI.Rule());Section(L.T("Ритм работы"));
  var sound=new CheckBox{Content=L.T("Звук при завершении квеста и таймера"),IsChecked=app.State.Sound};sound.Click+=(_,_)=>{app.State.Sound=sound.IsChecked==true;app.Commit(false);};tree.Children.Add(sound);
  var motion=new CheckBox{Content=L.T("Плавные переходы и свечение"),IsChecked=app.State.Motion};motion.Click+=(_,_)=>{app.State.Motion=motion.IsChecked==true;app.Commit(false);};tree.Children.Add(motion);
  var weathering=new CheckBox{Content=L.T("Потёртость и винтажный шэйдинг"),IsChecked=app.State.Weathering,ToolTip=L.T("Отдельный декоративный слой. Отключи, чтобы целиком убрать новую потёртость.")};weathering.Click+=(_,_)=>app.SetWeathering(weathering.IsChecked==true);tree.Children.Add(weathering);
  tree.Children.Add(UI.Rule());Section(L.T("Награды · Бусы"));
  tree.Children.Add(UI.SettingsNote(L.T("База по видам квестов. Собственную сумму можно задать через «Изменить» у задачи. Главный квест даёт отдельный бонус за полное завершение; шаги оплачиваются отдельно. Уже полученные награды не пересчитываются.")));
  AddRewardField(tree,L.T("Главный квест"),app.State.MainReward,v=>{app.State.MainReward=v;app.Commit(false);});
  AddRewardField(tree,L.T("Побочный квест"),app.State.SideReward,v=>{app.State.SideReward=v;app.Commit(false);});
  AddRewardField(tree,L.T("Шаг"),app.State.StepReward,v=>{app.State.StepReward=v;app.Commit(false);});
  tree.Children.Add(UI.Text(L.T("0 — без награды. Любая сумма до 100 000 бусин."),12,UI.Muted));
  tree.Children.Add(UI.Rule());Section(L.T("Твой журнал"));tree.Children.Add(UI.SettingsNote(L.T("Всё хранится локально. Предыдущая версия данных автоматически сохраняется в journal.json.bak.")));
  tree.Children.Add(UI.Button(L.T("Экспортировать резервную копию…"),()=>{
   var dialog=new Microsoft.Win32.SaveFileDialog{Filter=L.T("Журнал (*.json)|*.json"),FileName="silk-journal-"+DateTime.Now.ToString("yyyy-MM-dd")+".json"};if(dialog.ShowDialog(this)==true){try{if(!app.Commit(false))return;File.Copy(app.Store.FilePath,dialog.FileName,true);Toast(L.T("Резервная копия сохранена."));}catch(Exception ex){MessageBox.Show(this,ex.Message,L.T("Ошибка экспорта"));}}
  }));
  AddRestoreControl();
  tree.Children.Add(UI.Button(L.T("Закрыть приложение полностью"),()=>app.ExitApp()));
  details.Children.Add(UI.Text(L.T("Тихое место для твоих больших дел."),27,null,true));details.Children.Add(UI.Rule());details.Children.Add(UI.SettingsNote(L.T("Крестик основного окна прячет журнал в область уведомлений Windows. Висящий квест остаётся на экране.\n\nДвойной щелчок по значку в трее снова откроет журнал.\n\nДля выхода используй кнопку здесь или меню значка в трее.")));
 }
 static string KindLabel(QuestKind kind)=>kind switch{QuestKind.Auto=>L.T("Авто"),QuestKind.Main=>L.T("Главный"),QuestKind.Side=>L.T("Побочный"),_=>L.T("Шаг")};
 TextBox AddRewardField(Panel panel,string label,int value,Action<int> action,bool enabled=true)
 {
  var row=new Grid{Margin=new Thickness(0,12,0,8)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(105)});row.Children.Add(UI.Text(label,16,UI.Paper));
  int last=Math.Clamp(value,0,100000);var input=new TextBox{Text=last.ToString(),IsEnabled=enabled,FontSize=17,Padding=new Thickness(9,7,9,7),TextAlignment=TextAlignment.Right,MaxLength=6,VerticalContentAlignment=VerticalAlignment.Center};System.Windows.Automation.AutomationProperties.SetName(input,label);Grid.SetColumn(input,1);row.Children.Add(input);panel.Children.Add(row);
  input.PreviewTextInput+=(_,e)=>e.Handled=e.Text.Any(c=>c<'0'||c>'9');
  DataObject.AddPastingHandler(input,(_,e)=>{if(!e.DataObject.GetDataPresent(DataFormats.UnicodeText)||e.DataObject.GetData(DataFormats.UnicodeText) is not string text||!int.TryParse(text,out int n)||n<0||n>100000)e.CancelCommand();});
  input.TextChanged+=(_,_)=>{if(int.TryParse(input.Text,out int n)){last=Math.Clamp(n,0,100000);action(last);}};
  input.LostKeyboardFocus+=(_,_)=>input.Text=last.ToString();return input;
 }
 void AddSlider(string label,double min,double max,double value,Action<double> action,Func<double,string> format,bool integer=false)
 {
  var row=new Grid{Margin=new Thickness(0,14,0,10)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var text=UI.Text(label,16);text.Margin=new Thickness(0,0,14,0);row.Children.Add(text);var amount=UI.Text(format(value),14,UI.Muted);Grid.SetColumn(amount,1);row.Children.Add(amount);tree.Children.Add(row);
  var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=integer?1:.01,SmallChange=integer?1:.01,LargeChange=integer?5:.1,IsSnapToTickEnabled=integer,Margin=new Thickness(0,0,0,12),ToolTip=L.T("Перетаскивай метку или используй стрелки ← →. Home / End — крайние значения.")};
  System.Windows.Automation.AutomationProperties.SetName(slider,label);
  slider.ValueChanged+=(_,e)=>{amount.Text=format(e.NewValue);action(e.NewValue);};tree.Children.Add(slider);
 }
 void UpdateTimer()
 {
  string? id=app.State.SelectedId;if(timerText!=null)timerText.Text=app.TimerLabel(id);
  focusSpool?.SetRunning(app.Running&&app.State.FocusQuestId==id,app.State.Motion);
  focusSpool?.SetTime(app.State.FocusQuestId==id?app.State.FocusSeconds:app.State.FocusMinutes*60,app.FocusDuration(id),app.State.Motion);
  if(timerButton!=null){timerButton.Content=app.Running&&app.State.FocusQuestId==id?L.T("Ⅱ  Приостановить"):app.State.FocusQuestId==id&&app.State.FocusSeconds<app.FocusDuration(id)&&app.State.FocusSeconds>0?L.T("▶  Продолжить фокус"):L.T("▶  Начать фокус");System.Windows.Automation.AutomationProperties.SetName(timerButton,timerButton.Content.ToString());}
 }
}
