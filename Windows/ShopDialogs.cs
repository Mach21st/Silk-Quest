using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;

namespace SilkJournal;

sealed class ShopWindow : Window
{
 readonly Grid presentation=new();
 public StackPanel Body {get;}=new();
 public ScrollViewer BodyScroll {get;}
 public StackPanel Footer {get;}=new(){Margin=new Thickness(24,0,24,16)};
 public ShopWindow(Window owner,string title,double width,double height,bool preview=false)
 {
  Owner=owner;Title=title;Width=Math.Min(width,SystemParameters.WorkArea.Width-30);Height=Math.Min(height,SystemParameters.WorkArea.Height-30);MinWidth=Math.Min(380,Width);MinHeight=Math.Min(400,Height);WindowStartupLocation=WindowStartupLocation.CenterOwner;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;ShowInTaskbar=false;Icon=BrandAssets.WindowIcon();
  WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(6),CornerRadius=new CornerRadius(8),GlassFrameThickness=new Thickness(0)});
  var frame=new Border{Background=UI.Ink,BorderBrush=UI.B("#A09480"),BorderThickness=new Thickness(.8),CornerRadius=new CornerRadius(8)};Content=frame;
  var layers=new Grid();frame.Child=layers;layers.Children.Add(new JournalSurface());var layout=presentation;layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(42)});layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layers.Children.Add(layout);
  var header=new Grid{Background=UI.B("#171F26")};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});
  var label=UI.Text(title,16,UI.Paper);label.Margin=new Thickness(20,0,0,0);header.Children.Add(label);
  header.MouseLeftButtonDown+=(_,e)=>{if(e.LeftButton==MouseButtonState.Pressed)try{DragMove();}catch(InvalidOperationException){}};
  var close=UI.Button("×",Close);close.Style=(Style)FindResource("NavigationButton");close.Margin=new Thickness(0);close.Padding=new Thickness(5);Grid.SetColumn(close,1);header.Children.Add(close);layout.Children.Add(header);
  if(preview){header.Visibility=Visibility.Collapsed;layout.RowDefinitions[0].Height=new GridLength(0);ResizeMode=ResizeMode.NoResize;WindowChrome.GetWindowChrome(this).ResizeBorderThickness=new Thickness(0);}
  Body.Margin=new Thickness(0,0,10,0);var scroll=BodyScroll=UI.Scroll(Body,new Thickness(24,22,14,18));Grid.SetRow(scroll,1);layout.Children.Add(scroll);
  if(preview)
  {
   Body.Margin=new Thickness(26,0,26,0);scroll.Margin=new Thickness(24,24,24,12);
   scroll.Loaded+=(_,_)=>{
    scroll.ApplyTemplate();
    if(scroll.Template.FindName("PART_ScrollContentPresenter",scroll) is ScrollContentPresenter viewport)Grid.SetColumnSpan(viewport,2);
   };
  }
  Grid.SetRow(Footer,2);layout.Children.Add(Footer);
  WindowFrame.Attach(this,layers,8);PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};
 }
 public void AnimateOpening(bool motion)
 {
  if(!motion)return;
  var offset=new TranslateTransform(0,9);presentation.RenderTransform=offset;presentation.Opacity=0;
  Loaded+=(_,_)=>{
   presentation.Opacity=1;offset.Y=0;
   var duration=TimeSpan.FromMilliseconds(220);
   presentation.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,duration){FillBehavior=FillBehavior.Stop});
   offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(9,0,duration){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
  };
 }
 public void DismissWhenOutside(bool motion)
 {
  var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(60)};
  var owner=Owner;
  DateTime? outsideSince=null;bool fading=false,closing=false,closed=false,committedClose=false;
  bool Attached()=>!closing&&!closed&&IsVisible&&PresentationSource.FromVisual(this)?.CompositionTarget!=null;
  bool CursorInside()
  {
   if(!Attached())return false;
   var screenCursor=System.Windows.Forms.Cursor.Position;
   var cursor=PointFromScreen(new Point(screenCursor.X,screenCursor.Y));
   return cursor.X>=0&&cursor.Y>=0&&cursor.X<ActualWidth&&cursor.Y<ActualHeight;
  }
  void CancelFade()
  {
   fading=false;BeginAnimation(OpacityProperty,null);Opacity=1;
   if(presentation.RenderTransform is TranslateTransform offset){offset.BeginAnimation(TranslateTransform.YProperty,null);offset.Y=0;}
  }
  void Dismiss(bool committed)
  {
   if(!Attached())return;committedClose|=committed;
   if(fading)return;
   if(!motion){Close();return;}
   fading=true;
   var duration=TimeSpan.FromMilliseconds(220);
   var fade=new DoubleAnimation(Opacity,0,duration);
   fade.Completed+=(_,_)=>{if(fading&&Attached())Close();};
   BeginAnimation(OpacityProperty,fade);
   if(presentation.RenderTransform is TranslateTransform offset)
    offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(offset.Y,9,duration){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseIn}});
  }
  MouseEnter+=(_,_)=>{if(!Attached())return;outsideSince=null;if(fading&&!committedClose)CancelFade();};
  timer.Tick+=(_,_)=>{
   if(!Attached()){timer.Stop();return;}
   if(CursorInside()){outsideSince=null;if(fading&&!committedClose)CancelFade();return;}
   outsideSince??=DateTime.UtcNow;
   if((DateTime.UtcNow-outsideSince.Value).TotalMilliseconds>=1100)Dismiss(false);
  };
  void OutsideClick(object sender,MouseButtonEventArgs e){if(Attached()&&!CursorInside()){Dismiss(true);e.Handled=true;}}
  owner.PreviewMouseDown+=OutsideClick;
  Deactivated+=(_,_)=>{if(Attached()&&!CursorInside())Dismiss(true);};
  ContentRendered+=(_,_)=>{if(Attached())timer.Start();};
  Closing+=(_,_)=>{closing=true;fading=false;timer.Stop();owner.PreviewMouseDown-=OutsideClick;};
  Closed+=(_,_)=>{closed=true;timer.Stop();};
 }
}

static class ShopEditor
{
 public static void Show(MainWindow owner,App app,ShopItem? existing)
 {
  var dialog=new ShopWindow(owner,L.T(existing==null?"Новый товар":"Изменить товар"),590,800);
  var row=new Grid{Margin=new Thickness(0,0,0,14)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(65,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(35,GridUnitType.Star)});
  var nameGroup=new StackPanel();var name=new TextBox{Text=existing?.Name??"",MaxLength=100,FontSize=20,Margin=new Thickness(0,7,0,0)};nameGroup.Children.Add(UI.Label(L.T("НАЗВАНИЕ")));nameGroup.Children.Add(name);row.Children.Add(nameGroup);
  var price=new RosaryPriceStepper(existing?.Price??25){Margin=new Thickness(10,9,0,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(price,1);row.Children.Add(price);dialog.Body.Children.Add(row);
  dialog.Body.Children.Add(UI.Label(L.T("ИЗОБРАЖЕНИЕ · 1:1")));
  string image=existing?.Image??"";var crop=new ShopCropper{Margin=new Thickness(0,12,0,10)};dialog.Body.Children.Add(crop);
  if(image.Length>0)try{crop.SetImage(ShopImages.Decode(image),existing!.CropX,existing.CropY,existing.Zoom);}catch(Exception e)when(e is FormatException or IOException or NotSupportedException or ArgumentException){image="";}
  var error=UI.Text("",14,UI.Red);error.Margin=new Thickness(0,4,0,8);
  var choose=UI.Button(L.T("Выбрать изображение…"),()=>{
   var picker=new Microsoft.Win32.OpenFileDialog{Title=L.T("Выбрать изображение"),Filter=L.T("Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff"),CheckFileExists=true};
   if(picker.ShowDialog(dialog)!=true)return;
   try{string imported=ShopImages.Import(picker.FileName);var bitmap=ShopImages.Decode(imported);image=imported;crop.SetImage(bitmap);error.Text="";}
   catch(Exception e)when(e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FormatException or System.IO.FileFormatException){error.Text=L.T("Не удалось открыть изображение. Попробуй PNG или JPG.");}
  });choose.FontSize=16;choose.Padding=new Thickness(10,9,10,9);dialog.Body.Children.Add(choose);crop.ImageRequested+=()=>choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var zoom=new Slider{Minimum=1,Maximum=5,Value=crop.Zoom,SmallChange=.05,LargeChange=.25,Margin=new Thickness(0,2,0,5)};System.Windows.Automation.AutomationProperties.SetName(zoom,L.T("Масштаб изображения"));
  zoom.ValueChanged+=(_,e)=>{if(Math.Abs(crop.Zoom-e.NewValue)>.001)crop.SetZoom(e.NewValue);};crop.Changed+=()=>{if(Math.Abs(zoom.Value-crop.Zoom)>.001)zoom.Value=crop.Zoom;};dialog.Body.Children.Add(zoom);
  var cropTools=new Grid{Margin=new Thickness(0,0,0,14)};cropTools.ColumnDefinitions.Add(new ColumnDefinition());cropTools.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});cropTools.Children.Add(UI.Text(L.T("Тяни — положение · колесо — масштаб."),13,UI.Muted));
  var reset=UI.Button("↺",()=>{if(crop.Source!=null)crop.SetImage(crop.Source);});reset.ToolTip=L.T("Сбросить кроп");reset.FontSize=18;reset.Padding=new Thickness(6,0,6,0);reset.Margin=new Thickness(8,0,0,0);reset.Background=Brushes.Transparent;reset.BorderThickness=new Thickness(0);Grid.SetColumn(reset,1);cropTools.Children.Add(reset);dialog.Body.Children.Add(cropTools);
  dialog.Body.Children.Add(UI.Label(L.T("ПОДРОБНОСТИ")));
  var description=new TextBox{Text=existing?.Description??"",MaxLength=2000,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=80,MaxHeight=150,Margin=new Thickness(0,7,0,12)};dialog.Body.Children.Add(description);dialog.Footer.Children.Add(error);
  dialog.Footer.Children.Add(UI.Button(L.T("Сохранить товар"),()=>{
   if(string.IsNullOrWhiteSpace(name.Text)){error.Text=L.T("Добавь название товара.");name.Focus();return;}
   if(image.Length==0){error.Text=L.T("Выбери изображение товара.");return;}
   var item=new ShopItem{Id=existing?.Id??Guid.NewGuid().ToString("N"),Name=name.Text.Trim(),Description=description.Text.Trim(),Price=price.Value,Image=image,CropX=crop.X,CropY=crop.Y,Zoom=crop.Zoom};
   if(existing!=null)
   {
    var current=app.State.ShopItems.Find(x=>x.Id==existing.Id);
    var merged=MobileSync.MergeShopDraft(existing,item,current,out bool conflict);
    if(conflict&&MessageBox.Show(dialog,L.Language=="en"?"This item changed on your phone while the editor was open. Keep your edits to the conflicting fields? Other changes will be preserved.":"Пока редактор был открыт, товар изменился на телефоне. Оставить твои правки в спорных полях? Остальные изменения сохранятся.",L.T("Лавка"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
    item=merged;
   }
   if(app.SaveShopItem(item))dialog.Close();else error.Text=L.T("Не удалось сохранить товар. Проверь доступ к папке журнала.");
  },true));
  if(existing!=null)
  {
   var delete=UI.Button(L.T("Удалить товар…"),()=>{
    if(MessageBox.Show(dialog,L.F("Удалить товар «{0}»?",existing.Name),L.T("Лавка"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
    if(app.DeleteShopItem(existing))dialog.Close();
   });delete.Foreground=UI.Red;delete.FontSize=14;dialog.Footer.Children.Add(delete);
  }
  dialog.Loaded+=(_,_)=>name.Focus();dialog.ShowDialog();
 }
}

sealed class RosaryPriceStepper : Grid
{
 public int Value {get;private set;}
 readonly TextBlock amount=UI.Text("",20);
 readonly System.Collections.Generic.List<(Button Button,int Delta)> buttons=new();
 public RosaryPriceStepper(int value)
 {
  Value=Math.Clamp(value,0,1000000);RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var number=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};number.Children.Add(new RosaryIcon());amount.Margin=new Thickness(5,0,0,0);number.Children.Add(amount);
  var view=new Viewbox{Child=number,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,MaxHeight=30};Children.Add(view);
  var steps=new UniformGrid{Columns=4,Margin=new Thickness(0,3,0,0)};Grid.SetRow(steps,1);Children.Add(steps);
  foreach(int delta in new[]{-5,-1,1,5})
  {
   var button=UI.Button(delta<0?"−"+(-delta):"+"+delta,()=>Adjust(delta));button.Style=(Style)FindResource("NavigationButton");button.FontSize=14;button.Padding=new Thickness(2,4,2,4);button.Margin=new Thickness(0);button.ToolTip=L.F("Изменить цену на {0}",delta);buttons.Add((button,delta));steps.Children.Add(button);
  }
  Update();
 }
 void Adjust(int delta){Value=Math.Clamp(Value+delta,0,1000000);Update();}
 void Update(){amount.Text=Value.ToString("N0",L.Culture);foreach(var entry in buttons)entry.Button.IsEnabled=entry.Delta<0?Value>0:Value<1000000;System.Windows.Automation.AutomationProperties.SetName(this,L.F("Цена: {0}",L.Beads(Value)));}
}
