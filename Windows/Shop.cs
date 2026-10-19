using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SilkJournal;

public sealed class ShopItem
{
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public string Name {get;set;}="";
 public string Description {get;set;}="";
 public int Price {get;set;}=25;
 public string Image {get;set;}="";
 public double CropX {get;set;}=.5;
 public double CropY {get;set;}=.5;
 public double Zoom {get;set;}=1;
}

public sealed class ShopPurchase
{
 public string Id {get;set;}="";
 public string ItemId {get;set;}="";
 public string Name {get;set;}="";
 public int Price {get;set;}
 public DateTimeOffset BoughtAt {get;set;}
}
public enum PurchaseResult { Purchased, AlreadyPurchased, NotEnough, Missing, SaveFailed }

public partial class App
{
 public bool SaveShopItem(ShopItem item)
 {
  if(item.Price<0||item.Price>1000000)return false;
  int index=State.ShopItems.FindIndex(p=>p.Id==item.Id);ShopItem? before=index<0?null:State.ShopItems[index];
  if(index<0)State.ShopItems.Add(item);else State.ShopItems[index]=item;
  if(!Commit(false)){if(index<0)State.ShopItems.Remove(item);else State.ShopItems[index]=before!;return false;}
  RefreshViews();return true;
 }
 public bool DeleteShopItem(ShopItem item)
 {
  int index=State.ShopItems.FindIndex(p=>p.Id==item.Id);if(index<0)return false;
  var before=State.ShopItems[index];State.ShopItems.RemoveAt(index);
  if(!Commit(false)){State.ShopItems.Insert(index,before);return false;}RefreshViews();return true;
 }
 public PurchaseResult BuyShopItem(string itemId,string operationId,bool notifyFailure=true)
 {
  if(State.ShopPurchases.Any(p=>p.Id==operationId))return PurchaseResult.AlreadyPurchased;
  var item=State.ShopItems.FirstOrDefault(p=>p.Id==itemId);if(item==null||item.Price<0||item.Price>1000000)return PurchaseResult.Missing;
  if(State.Rosaries<item.Price)return PurchaseResult.NotEnough;
  long before=State.Rosaries;var purchase=new ShopPurchase{Id=operationId,ItemId=item.Id,Name=item.Name,Price=item.Price,BoughtAt=DateTimeOffset.Now};
  State.Rosaries-=item.Price;State.ShopPurchases.Add(purchase);
  if(!Commit(false,notifyFailure)){State.Rosaries=before;State.ShopPurchases.Remove(purchase);return PurchaseResult.SaveFailed;}
  RefreshViews();return PurchaseResult.Purchased;
 }
}

public sealed partial class MainWindow
{
 Grid shopMainArea=null!,shopDetailArea=null!;
 void SetShopLayout(bool shop)
 {
  Grid.SetColumnSpan(shopMainArea,shop?2:1);shopDetailArea.Visibility=shop?Visibility.Collapsed:Visibility.Visible;
 }
 void RenderShop()
 {
  var grid=new UniformGrid{Columns=4};
  foreach(var item in app.State.ShopItems)grid.Children.Add(ShopTile(item));
  grid.Children.Add(ShopTile(null));tree.Children.Add(grid);
  if(app.State.ShopPurchases.Count>0)
  {
   var history=new Expander{Header=L.F("Покупки · {0}",app.State.ShopPurchases.Count),Foreground=UI.Paper,FontFamily=new FontFamily("Cambria"),FontSize=18,Margin=new Thickness(0,15,0,12)};
   var receipts=new StackPanel{Margin=new Thickness(0,12,0,0)};history.Content=receipts;
   foreach(var purchase in app.State.ShopPurchases.AsEnumerable().Reverse().Take(50))
   {
    var row=new Grid{Margin=new Thickness(0,0,0,12)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var text=new StackPanel();text.Children.Add(UI.Text(purchase.Name,18));text.Children.Add(UI.Text(purchase.BoughtAt.ToLocalTime().ToString("g",L.Culture),12,UI.Muted));row.Children.Add(text);
    var amount=UI.Text("−"+L.Beads(purchase.Price),15,UI.Muted);Grid.SetColumn(amount,1);row.Children.Add(amount);receipts.Children.Add(row);
   }
   tree.Children.Add(history);
  }
 }
 FrameworkElement ShopTile(ShopItem? item)
 {
  var card=new Border{Background=new LinearGradientBrush(UI.Color("#1D2830"),UI.Color("#111A22"),90),BorderBrush=UI.B("#58554E"),BorderThickness=new Thickness(.8),CornerRadius=new CornerRadius(10),Padding=new Thickness(8),RenderTransformOrigin=new Point(.5,.5)};
  var content=new StackPanel();card.Child=content;
  var art=new ShopArt{Image=item==null?null:ShopImages.Preview(item),Placeholder=item==null};content.Children.Add(art);
  var name=UI.Text(item?.Name??L.T("Добавить товар"),17,UI.Paper);name.LineHeight=21;name.MaxHeight=42;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new Thickness(3,12,3,6);name.VerticalAlignment=VerticalAlignment.Top;content.Children.Add(name);
  if(item!=null){var price=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(3,0,3,4)};price.Children.Add(new Viewbox{Width=16,Height=19,Child=new RosaryIcon(),Margin=new Thickness(0,0,6,0)});price.Children.Add(UI.Text(L.Beads(item.Price),15,UI.Muted));content.Children.Add(price);}
  var button=UI.Button(item?.Name??L.T("Добавить товар"),()=>{if(item==null)EditShopItem(null);else ShowShopItem(item);});
  // Keep the transformed artwork inside the button's fixed layout bounds,
  // including at the clipped edges of the scrolling viewport.
  button.Style=(Style)FindResource("NavigationButton");button.Padding=new Thickness(0);button.Margin=new Thickness(0,0,0,4);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.VerticalContentAlignment=VerticalAlignment.Stretch;
  var motionArea=new Grid();card.Margin=new Thickness(10);motionArea.Children.Add(card);button.Content=motionArea;
  button.ToolTip=item?.Name??L.T("Добавить товар");
  var move=new TranslateTransform();var rotate=new RotateTransform();var transform=new TransformGroup();transform.Children.Add(rotate);transform.Children.Add(move);card.RenderTransform=transform;
  void Return()
  {
   double x=move.X,y=move.Y,a=rotate.Angle;move.BeginAnimation(TranslateTransform.XProperty,null);move.BeginAnimation(TranslateTransform.YProperty,null);rotate.BeginAnimation(RotateTransform.AngleProperty,null);move.X=move.Y=rotate.Angle=0;
   if(app.State.Motion){var duration=TimeSpan.FromMilliseconds(180);var ease=new CubicEase{EasingMode=EasingMode.EaseOut};move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(x,0,duration){EasingFunction=ease});move.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(y,0,duration){EasingFunction=ease});rotate.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(a,0,duration){EasingFunction=ease});}
  }
  button.MouseMove+=(_,e)=>{
   if(!app.State.Motion)return;var p=e.GetPosition(button);double x=Math.Clamp(p.X/Math.Max(1,button.ActualWidth)*2-1,-1,1),y=Math.Clamp(p.Y/Math.Max(1,button.ActualHeight)*2-1,-1,1);
   double angle=Math.Min(1.1,600/Math.Max(1,Math.Max(card.ActualWidth,card.ActualHeight)));
   move.BeginAnimation(TranslateTransform.XProperty,null);move.BeginAnimation(TranslateTransform.YProperty,null);rotate.BeginAnimation(RotateTransform.AngleProperty,null);move.X=-x*3;move.Y=-y*3;rotate.Angle=-x*angle;
  };
  button.MouseLeave+=(_,_)=>Return();
  return button;
 }
 void ShowShopItem(ShopItem item)
 {
  var dialog=new ShopWindow(this,item.Name,520,740,preview:true);
  dialog.AnimateOpening(app.State.Motion);
  dialog.DismissWhenOutside(app.State.Motion);
  var title=UI.Text(item.Name,29);title.TextAlignment=TextAlignment.Center;title.Margin=new Thickness(0,0,0,20);dialog.Body.Children.Add(title);
  var art=new ShopArt{Image=ShopImages.Preview(item),MaxWidth=350,HorizontalAlignment=HorizontalAlignment.Center};dialog.Body.Children.Add(art);
  var description=UI.Text(item.Description,18,UI.Muted);description.TextAlignment=TextAlignment.Center;description.Margin=new Thickness(0,16,0,16);
  var descriptionSpace=new Border{Child=description,MinHeight=88};dialog.Body.Children.Add(descriptionSpace);
  void CenterDescription()
  {
   double space=Math.Max(88,dialog.BodyScroll.ViewportHeight-title.DesiredSize.Height-art.DesiredSize.Height);
   if(double.IsFinite(space)&&Math.Abs(descriptionSpace.MinHeight-space)>.5)descriptionSpace.MinHeight=space;
  }
  dialog.BodyScroll.ScrollChanged+=(_,e)=>{if(e.ViewportHeightChange!=0)CenterDescription();};
  EventHandler layoutUpdated=(_,_)=>CenterDescription();dialog.Body.LayoutUpdated+=layoutUpdated;
  dialog.Closed+=(_,_)=>dialog.Body.LayoutUpdated-=layoutUpdated;
  dialog.Footer.Children.Add(UI.Rule(10));
  var balance=UI.Text(L.F("Твой баланс: {0}",L.Beads(app.State.Rosaries)),14,UI.Muted);balance.TextAlignment=TextAlignment.Center;balance.Margin=new Thickness(0,0,0,12);dialog.Footer.Children.Add(balance);
  var error=UI.Text("",14,UI.Red);error.TextAlignment=TextAlignment.Center;dialog.Footer.Children.Add(error);string operationId=Guid.NewGuid().ToString("N");
  var buy=UI.Button(L.F("Купить · {0}",L.Beads(item.Price)),()=>{
   var result=app.BuyShopItem(item.Id,operationId,false);
   if(result is PurchaseResult.Purchased or PurchaseResult.AlreadyPurchased){dialog.Close();Toast(L.F("Куплено: {0}",item.Name));}
   else error.Text=L.T(result==PurchaseResult.NotEnough?"Недостаточно бусин.":result==PurchaseResult.Missing?"Товар больше недоступен.":"Покупка не сохранена. Баланс не списан.");
  },true);
  buy.IsEnabled=app.State.Rosaries>=item.Price;dialog.Footer.Children.Add(buy);
  if(!buy.IsEnabled)error.Text=L.F("Не хватает {0}",L.Beads(item.Price-app.State.Rosaries,true));
  void UpdateAccount(){balance.Text=L.F("Твой баланс: {0}",L.Beads(app.State.Rosaries));buy.IsEnabled=app.State.Rosaries>=item.Price;error.Text=buy.IsEnabled?"":L.F("Не хватает {0}",L.Beads(item.Price-app.State.Rosaries,true));}
  app.Changed+=UpdateAccount;dialog.Closed+=(_,_)=>app.Changed-=UpdateAccount;
  var edit=UI.Button(L.T("Изменить  ···"),()=>{dialog.Close();EditShopItem(item);});edit.FontSize=14;edit.Padding=new Thickness(8);dialog.Footer.Children.Add(edit);dialog.Show();
 }
 void EditShopItem(ShopItem? existing)
 {
  FlushEditors();ShopEditor.Show(this,app,existing);
 }
}

sealed class ShopArt : FrameworkElement
{
 public ImageSource? Image {get;set;}
 public bool Placeholder {get;set;}
 protected override Size MeasureOverride(Size available)
 {
  double width=double.IsFinite(available.Width)?Math.Min(available.Width,MaxWidth):180;return new Size(width,width);
 }
 protected override void OnRender(DrawingContext d)
 {
  var rect=new Rect(0,0,ActualWidth,ActualHeight);if(rect.IsEmpty)return;
  Brush fill=Image==null?UI.B("#101922"):new ImageBrush(Image){Stretch=Stretch.UniformToFill};d.DrawRoundedRectangle(fill,InkDrawing.Pen("#55564F",.7),rect,7,7);
  if(Image==null){double x=ActualWidth/2,y=ActualHeight/2;var pen=InkDrawing.Pen("#C0AF94",1);d.DrawLine(pen,new Point(x-13,y),new Point(x+13,y));d.DrawLine(pen,new Point(x,y-13),new Point(x,y+13));}
 }
}
