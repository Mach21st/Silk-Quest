using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SilkJournal;

sealed class RosaryCounter : TextBlock
{
 public static readonly DependencyProperty DisplayValueProperty=DependencyProperty.Register(nameof(DisplayValue),typeof(double),typeof(RosaryCounter),new PropertyMetadata(0d,(d,e)=>((RosaryCounter)d).Text=((double)e.NewValue).ToString("N0",L.Culture)));
 public double DisplayValue{get=>(double)GetValue(DisplayValueProperty);set=>SetValue(DisplayValueProperty,value);}
 long amount;bool initialized;string? culture;
 public RosaryCounter(){Text="0";FontSize=20;FontFamily=new FontFamily("Cambria");Foreground=UI.Paper;VerticalAlignment=VerticalAlignment.Center;MinWidth=35;TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);}
 public void SetAmount(long value,bool motion)
 {
  if(initialized&&amount==value){if(culture!=L.Culture.Name)Text=value.ToString("N0",L.Culture);culture=L.Culture.Name;return;}double from=DisplayValue;BeginAnimation(DisplayValueProperty,null);DisplayValue=value;
  if(initialized&&motion)BeginAnimation(DisplayValueProperty,new DoubleAnimation(from,value,TimeSpan.FromMilliseconds(480)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
  initialized=true;amount=value;culture=L.Culture.Name;
 }
}

public sealed partial class MainWindow
{
 ScrollViewer tasksScroller=null!,detailScroller=null!;
 string? renderedPage,renderedDetail;
 bool restoringView;
 int viewGeneration;
 bool lastNarrow;
 bool CompactLayout=>(ActualWidth>0?ActualWidth:Width)<1080;
 readonly DispatcherTimer viewSaveTimer=new(){Interval=TimeSpan.FromMilliseconds(650)};
 public void CancelEditors(){editingId=null;titleEditor=null;notesEditor=null;editMode=false;}
 public void ReloadViewPreferences(){page=Pages.Contains(app.State.CurrentPage)?app.State.CurrentPage:"Сегодня";expanded.Clear();foreach(string id in app.State.ExpandedQuestIds)if(app.State.Find(id)!=null)expanded.Add(id);}
 void InitViewMemory()
 {
  viewSaveTimer.Tick+=(_,_)=>{viewSaveTimer.Stop();app.Commit(false);};
  void Changed(object sender,ScrollChangedEventArgs e){if(restoringView||rendering||Math.Abs(e.VerticalChange)<.01)return;CaptureViewState();viewSaveTimer.Stop();viewSaveTimer.Start();}
  tasksScroller.ScrollChanged+=Changed;detailScroller.ScrollChanged+=Changed;
 }
 public void CaptureViewState()
 {
  app.State.CurrentPage=page;app.State.ExpandedQuestIds=expanded.Where(id=>app.State.Find(id)!=null).ToHashSet();
  if(restoringView)return;
  if(renderedPage!=null&&tasksScroller!=null)app.State.PageScrollOffsets[renderedPage]=tasksScroller.VerticalOffset;
  if(renderedDetail!=null&&detailScroller!=null)app.State.DetailScrollOffsets[renderedDetail]=detailScroller.VerticalOffset;
 }
 void BeginViewRender(){CaptureViewState();restoringView=true;viewGeneration++;}
 void FinishViewRender()
 {
  string key=page+"|"+app.State.SelectedId+"|"+editMode;renderedPage=page;renderedDetail=key;int generation=viewGeneration;
  double taskOffset=app.State.PageScrollOffsets.GetValueOrDefault(page),detailOffset=app.State.DetailScrollOffsets.GetValueOrDefault(key);
  Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{
   if(generation!=viewGeneration)return;UpdateLayout();tasksScroller.ScrollToVerticalOffset(taskOffset);detailScroller.ScrollToVerticalOffset(detailOffset);
   Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{if(generation==viewGeneration){restoringView=false;CaptureViewState();}}));
  }));
 }
 void UpdateUndoButton(){undoButton.Visibility=app.CanUndo?Visibility.Visible:Visibility.Collapsed;undoButton.ToolTip=app.UndoLabel+" · Ctrl+Z";}
 public void UpdateSaveIndicator(){if(!app.SaveHealthy){toastTimer.Stop();toast.Text=DefaultStatus();}else if(toast.Text.StartsWith("⚠",StringComparison.Ordinal))toast.Text=DefaultStatus();}
 void ShowRewardGain(int earned)
 {
  if(!app.State.Motion)return;rewardGain.Text="+"+earned.ToString("N0",L.Culture);
  rewardGain.RenderTransform=new TranslateTransform();rewardGain.BeginAnimation(OpacityProperty,new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(1100),KeyFrames={new LinearDoubleKeyFrame(0,KeyTime.FromPercent(0)),new LinearDoubleKeyFrame(1,KeyTime.FromPercent(.12)),new LinearDoubleKeyFrame(1,KeyTime.FromPercent(.6)),new LinearDoubleKeyFrame(0,KeyTime.FromPercent(1))}});
  rewardGain.RenderTransform.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(-4,-27,TimeSpan.FromMilliseconds(1100)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
 }
 void FitCompactLayout(Grid body,Grid side)
 {
  bool narrow=CompactLayout,shortWindow=ActualHeight<740;body.ColumnDefinitions[0].MinWidth=narrow?215:222;body.ColumnDefinitions[1].MinWidth=narrow?300:410;body.ColumnDefinitions[2].MinWidth=narrow?280:350;
  compassHost.Width=compassHost.Height=shortWindow?92:148;daily.FontSize=shortWindow?16:19;side.Margin=shortWindow?new Thickness(0,16,0,12):new Thickness(0,30,0,18);
  if(narrow!=lastNarrow){lastNarrow=narrow;Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(Render));}
 }
 void AddRestoreControl()
 {
  var snapshots=app.Store.Snapshots();var expander=new Expander{Header=L.T("Восстановление · ")+snapshots.Length+L.T(" копий"),Foreground=UI.Muted,FontFamily=new FontFamily("Cambria"),FontSize=14,Margin=new Thickness(0,3,0,12)};var panel=new StackPanel{Margin=new Thickness(0,12,0,0)};expander.Content=panel;
  panel.Children.Add(UI.Text(L.T("Предыдущее сохранение и до 7 дневных снимков. Восстанавливаются квесты, лавка и баланс."),13,UI.Muted));
  foreach(var snapshot in snapshots){var restore=UI.Button("↶  "+snapshot.Label,()=>{
   if(MessageBox.Show(this,L.T("Восстановить квесты, лавку и баланс из «")+snapshot.Label+L.T("»? Текущее состояние сохранится в предыдущей копии. Настройки связи останутся."),L.T("Восстановление журнала"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
   try{app.RestoreSnapshot(snapshot.Path);}catch(Exception ex){Toast(L.T("Не удалось прочитать снимок: ")+ex.Message);}
  });restore.FontSize=14;restore.HorizontalContentAlignment=HorizontalAlignment.Left;restore.Margin=new Thickness(0,6,0,0);panel.Children.Add(restore);}
  if(app.Store.BackupWarning!=null)panel.Children.Add(UI.Text(app.Store.BackupWarning,12,UI.Muted));tree.Children.Add(expander);
 }
 internal double TaskScrollOffset=>tasksScroller.VerticalOffset;
 internal void SetTaskScroll(double value)=>tasksScroller.ScrollToVerticalOffset(value);
 internal bool UndoVisible=>undoButton.Visibility==Visibility.Visible;
 internal string RewardText=>rosaries.Text;
}
