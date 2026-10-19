using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SilkJournal;

static class ShopTests
{
 public static void Run(App app,Action<int> pump)
 {
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  ShopWindow Preview(bool motion)
  {
   var preview=new ShopWindow(app.Journal,"Lifecycle test",400,440,preview:true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000};
   preview.AnimateOpening(motion);preview.DismissWhenOutside(motion);return preview;
  }
  for(int i=0;i<4;i++)
  {
   var preview=Preview(i%2==0);preview.Show();if(i%2==0)pump(80);preview.Close();pump(160);
   Check(!preview.IsVisible,"Closing preview before or after first render leaves no active cursor callback");
  }
  var hiddenPreview=Preview(true);hiddenPreview.Show();pump(80);hiddenPreview.Hide();pump(160);hiddenPreview.Close();pump(120);
  foreach(bool motion in new[]{false,true})
  {
   var preview=Preview(motion);var area=SystemParameters.WorkArea;
   var mouse=System.Windows.Forms.Cursor.Position;var cursor=PresentationSource.FromVisual(app.Journal)!.CompositionTarget.TransformFromDevice.Transform(new Point(mouse.X,mouse.Y));
   preview.Left=cursor.X<area.Left+area.Width/2?area.Right-preview.Width-8:area.Left+8;preview.Top=area.Top+8;
   preview.Show();pump(2000);Check(!preview.IsVisible,"Outside timeout closes preview with motion "+motion);pump(180);
  }
  byte[] pixels=new byte[200*100*4];for(int y=0;y<100;y++)for(int x=0;x<200;x++){int i=(y*200+x)*4;pixels[i]=(byte)(x<100?0:255);pixels[i+2]=(byte)(x<100?255:0);pixels[i+3]=255;}
  var source=BitmapSource.Create(200,100,96,96,PixelFormats.Bgra32,null,pixels,800);source.Freeze();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var stream=new MemoryStream();encoder.Save(stream);string image=Convert.ToBase64String(stream.ToArray());
  string importFile=Path.Combine(Path.GetDirectoryName(app.Store.FilePath)!,"shop-import-test.png");File.WriteAllBytes(importFile,stream.ToArray());
  string imported=ShopImages.Import(importFile);using(var exclusive=File.Open(importFile,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}File.Delete(importFile);
  Check(ShopImages.Decode(imported).PixelWidth==200,"Import preserves small image resolution and releases the source file");
  byte[] Pixel(BitmapSource bitmap){byte[] p=new byte[4];bitmap.CopyPixels(new Int32Rect(256,256,1,1),p,4,0);return p;}
  var left=Pixel(ShopImages.Crop(source,0,.5,1));var right=Pixel(ShopImages.Crop(source,1,.5,1));Check(left[2]==255&&left[0]==0&&right[0]==255&&right[2]==0,"Square crop selects the requested part of a landscape image");
  var rect=ShopImages.SourceRect(source,1,0,5);Check(rect.Width==rect.Height&&rect.X+rect.Width<=200&&rect.Y>=0,"Zoomed crop remains square and inside source");
  long balance=app.State.Rosaries;string quests=System.Text.Json.JsonSerializer.Serialize(app.State.Quests);var added=new System.Collections.Generic.List<ShopItem>();
  for(int i=0;i<5;i++){var item=new ShopItem{Name="Shop test "+i,Description="Details",Image=image,CropX=1,Zoom=2};Check(app.SaveShopItem(item),"Shop item saves");added.Add(item);}
  var stored=app.Store.Load().ShopItems.First(p=>p.Id==added[0].Id);Check(stored.Image==image&&stored.CropX==1&&stored.Zoom==2&&ShopImages.Preview(stored)!.PixelWidth==512,"Original image and crop survive journal roundtrip");
  app.Journal.ShowShop();pump(250);var grid=FindGrid(app.Journal)??throw new Exception("Shop grid missing");Check(grid.Columns==4,"Shop uses four columns");
  var a=(FrameworkElement)grid.Children[0];var b=(FrameworkElement)grid.Children[3];var next=(FrameworkElement)grid.Children[4];Check(a.ActualWidth>80&&Math.Abs(a.TranslatePoint(new Point(),grid).Y-b.TranslatePoint(new Point(),grid).Y)<1&&next.TranslatePoint(new Point(),grid).Y>a.TranslatePoint(new Point(),grid).Y,"Four measured cards fit on one row and the next wraps");
  app.State.Rosaries=30;app.Commit(false);int historyCount=app.State.ShopPurchases.Count;string operation=Guid.NewGuid().ToString("N");
  using(var locked=File.Open(app.Store.FilePath+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
   Check(app.BuyShopItem(added[0].Id,operation,false)==PurchaseResult.SaveFailed&&app.State.Rosaries==30&&app.State.ShopPurchases.Count==historyCount,"Failed save rolls back purchase and balance");
  Check(app.BuyShopItem(added[0].Id,operation,false)==PurchaseResult.Purchased&&app.State.Rosaries==5,"Purchase debits exact price");
  Check(app.BuyShopItem(added[0].Id,operation,false)==PurchaseResult.AlreadyPurchased&&app.State.Rosaries==5,"Repeated purchase request cannot double charge");
  Check(app.BuyShopItem(added[0].Id,Guid.NewGuid().ToString("N"),false)==PurchaseResult.NotEnough&&app.State.Rosaries==5,"Insufficient balance cannot purchase");
  var paid=app.Store.Load();Check(paid.ShopPurchases.Any(p=>p.Id==operation&&p.Price==25&&p.Name==added[0].Name)&&paid.Rosaries==5,"Receipt and reduced balance are saved together");
  Check(app.RestoreSnapshot(app.Store.FilePath+".bak")&&app.State.Rosaries==30&&app.State.ShopPurchases.Count==historyCount,"Snapshot restores purchase history and balance together");
  app.State.Rosaries=balance;app.Commit(false);
  foreach(var item in added)Check(app.DeleteShopItem(item),"Shop item deletes");
  Check(app.State.Rosaries==balance&&System.Text.Json.JsonSerializer.Serialize(app.State.Quests)==quests,"Catalog editing preserves rewards and quests");app.Journal.ShowSettings();
 }
 static UniformGrid? FindGrid(DependencyObject root)
 {
  if(root is UniformGrid grid)return grid;
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindGrid(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}return null;
 }
}

public sealed partial class MainWindow
{
 internal void ShowShop()=>Navigate("Лавка");
}
