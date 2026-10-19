using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SilkJournal;

static class ShopImages
{
 static readonly ConditionalWeakTable<ShopItem,BitmapSource> cache=new();
 public static BitmapSource? Preview(ShopItem item)
 {
  if(string.IsNullOrEmpty(item.Image))return null;
  try{return cache.GetValue(item,p=>Crop(Decode(p.Image),p.CropX,p.CropY,p.Zoom));}
  catch(Exception e)when(e is FormatException or IOException or NotSupportedException or ArgumentException){return null;}
 }
 public static BitmapSource Decode(string data)
 {
  using var stream=new MemoryStream(Convert.FromBase64String(data));return Load(stream);
 }
 static BitmapSource Load(Stream stream)
 {
  var metadata=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand).Frames[0];
  int width=Math.Max(1,(int)(metadata.PixelWidth*Math.Min(1,1600d/Math.Max(metadata.PixelWidth,metadata.PixelHeight))));stream.Position=0;
  var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=width;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();return bitmap;
 }
 public static string Import(string file)
 {
  if(new FileInfo(file).Length>24*1024*1024)throw new IOException(L.T("Изображение слишком большое. Максимум — 24 МБ."));
  using var input=File.OpenRead(file);var bitmap=Load(input);
  string extension=Path.GetExtension(file).ToLowerInvariant();BitmapEncoder encoder=extension is ".jpg" or ".jpeg"?new JpegBitmapEncoder{QualityLevel=92}:new PngBitmapEncoder();
  encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=new MemoryStream();encoder.Save(output);return Convert.ToBase64String(output.ToArray());
 }
 public static Rect SourceRect(BitmapSource source,double x,double y,double zoom)
 {
  double size=Math.Min(source.PixelWidth,source.PixelHeight)/Math.Clamp(zoom,1,5);
  return new Rect(Math.Clamp(x,0,1)*(source.PixelWidth-size),Math.Clamp(y,0,1)*(source.PixelHeight-size),size,size);
 }
 public static BitmapSource Crop(BitmapSource source,double x,double y,double zoom)
 {
  var rect=SourceRect(source,x,y,zoom);double scale=512/rect.Width;var visual=new DrawingVisual();
  using(var d=visual.RenderOpen()){d.PushClip(new RectangleGeometry(new Rect(0,0,512,512)));d.DrawImage(source,new Rect(-rect.X*scale,-rect.Y*scale,source.PixelWidth*scale,source.PixelHeight*scale));d.Pop();}
  var result=new RenderTargetBitmap(512,512,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
 }
}

sealed class ShopCropper : FrameworkElement
{
 public BitmapSource? Source {get;private set;}
 public double X {get;private set;}=.5;
 public double Y {get;private set;}=.5;
 public double Zoom {get;private set;}=1;
 Point? start;
 double initialX,initialY;
 public event Action? Changed;
 public event Action? ImageRequested;
 public ShopCropper()
 {
  HorizontalAlignment=HorizontalAlignment.Stretch;Focusable=true;Cursor=Cursors.Hand;
  MouseLeftButtonDown+=(_,e)=>{if(Source==null){ImageRequested?.Invoke();e.Handled=true;return;}Focus();start=e.GetPosition(this);initialX=X;initialY=Y;CaptureMouse();e.Handled=true;};
  MouseMove+=(_,e)=>{
   if(start==null||Source==null||e.LeftButton!=MouseButtonState.Pressed)return;
   var p=e.GetPosition(this);var crop=ShopImages.SourceRect(Source,X,Y,Zoom);double scale=ActualWidth/crop.Width;
   double roomX=(Source.PixelWidth-crop.Width)*scale,roomY=(Source.PixelHeight-crop.Height)*scale;
   X=roomX>0?Math.Clamp(initialX-(p.X-start.Value.X)/roomX,0,1):.5;Y=roomY>0?Math.Clamp(initialY-(p.Y-start.Value.Y)/roomY,0,1):.5;InvalidateVisual();Changed?.Invoke();
  };
  MouseLeftButtonUp+=(_,e)=>{start=null;ReleaseMouseCapture();e.Handled=true;};LostMouseCapture+=(_,_)=>start=null;
  MouseWheel+=(_,e)=>{if(Source==null)return;SetZoom(Zoom*(e.Delta>0?1.08:1/1.08));e.Handled=true;};
  KeyDown+=(_,e)=>{
   double step=(Keyboard.Modifiers&ModifierKeys.Shift)!=0?.1:.02;
   if(e.Key==Key.Left)X=Math.Max(0,X-step);else if(e.Key==Key.Right)X=Math.Min(1,X+step);else if(e.Key==Key.Up)Y=Math.Max(0,Y-step);else if(e.Key==Key.Down)Y=Math.Min(1,Y+step);else return;
   e.Handled=true;InvalidateVisual();Changed?.Invoke();
  };
 }
 public void SetImage(BitmapSource source,double x=.5,double y=.5,double zoom=1){Source=source;X=Math.Clamp(x,0,1);Y=Math.Clamp(y,0,1);Zoom=Math.Clamp(zoom,1,5);InvalidateVisual();Changed?.Invoke();}
 public void SetZoom(double value){Zoom=Math.Clamp(value,1,5);InvalidateVisual();Changed?.Invoke();}
 protected override Size MeasureOverride(Size available)
 {
  double side=double.IsFinite(available.Width)?Math.Max(1,available.Width):270;return new Size(side,side);
 }
 protected override void OnRender(DrawingContext d)
 {
  var bounds=new Rect(0,0,ActualWidth,ActualHeight);d.DrawRoundedRectangle(UI.B("#0E171F"),null,bounds,8,8);d.PushClip(new RectangleGeometry(bounds,8,8));
  if(Source!=null)
  {
   var crop=ShopImages.SourceRect(Source,X,Y,Zoom);double scale=ActualWidth/crop.Width;
   d.DrawImage(Source,new Rect(-crop.X*scale,-crop.Y*scale,Source.PixelWidth*scale,Source.PixelHeight*scale));
   var guide=InkDrawing.Pen("#607C7568",.6);for(int i=1;i<=2;i++){double p=ActualWidth*i/3;d.DrawLine(guide,new Point(p,0),new Point(p,ActualHeight));d.DrawLine(guide,new Point(0,p),new Point(ActualWidth,p));}
  }
  else {var text=new FormattedText("+",L.Culture,FlowDirection.LeftToRight,new Typeface("Cambria"),36,UI.Paper,VisualTreeHelper.GetDpi(this).PixelsPerDip);d.DrawText(text,new Point((ActualWidth-text.Width)/2,(ActualHeight-text.Height)/2));}
  d.Pop();d.DrawRoundedRectangle(null,InkDrawing.Pen("#A4947D",1),bounds,8,8);
 }
}
