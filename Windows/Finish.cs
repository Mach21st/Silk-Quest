using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SilkJournal;

// The new worn finish is one independent, non-interactive layer.
// Disabling it leaves all previously approved artwork and colours intact.
sealed class PatinaLayer : FrameworkElement
{
 readonly App app;
 static readonly DrawingBrush Scuffs=MakeScuffs();
 public PatinaLayer(App app)
 {
  this.app=app;IsHitTestVisible=false;Focusable=false;
  Loaded+=(_,_)=>{app.AppearanceChanged+=Refresh;Refresh();};
  Unloaded+=(_,_)=>app.AppearanceChanged-=Refresh;
 }
 void Refresh(){Opacity=app.State.Weathering?1:0;}
 protected override void OnRender(DrawingContext d)
 {
  double w=ActualWidth,h=ActualHeight;if(w<1||h<1)return;
  var shade=new RadialGradientBrush();shade.Center=shade.GradientOrigin=new Point(.48,.4);shade.RadiusX=.78;shade.RadiusY=.85;
  shade.GradientStops.Add(new GradientStop(Colors.Transparent,.32));shade.GradientStops.Add(new GradientStop(UI.Color("#1504070B"),1));d.DrawRectangle(shade,null,new Rect(0,0,w,h));
  d.DrawRectangle(Scuffs,null,new Rect(0,0,w,h));
  var random=new Random(194);for(int i=0;i<60;i++)
  {
   double x=8+random.NextDouble()*Math.Max(1,w-16);double length=1+random.NextDouble()*7;double y=i%2==0?1.5:h-1.5;
   d.DrawLine(InkDrawing.Pen("#24D7C7AA",.45),new Point(x,y),new Point(x+length,y+.25));
  }
 }
 static DrawingBrush MakeScuffs()
 {
  var group=new DrawingGroup();var random=new Random(2207);using(var d=group.Open())
  {
   for(int i=0;i<430;i++){double x=random.NextDouble()*256,y=random.NextDouble()*256;bool pale=i%3==0;var brush=new SolidColorBrush(Color.FromArgb((byte)random.Next(3,13),pale?(byte)211:(byte)3,pale?(byte)198:(byte)8,pale?(byte)171:(byte)13));d.DrawEllipse(brush,null,new Point(x,y),.35+random.NextDouble()*.7,.25+random.NextDouble()*.4);}
   for(int i=0;i<35;i++){double x=random.NextDouble()*256,y=random.NextDouble()*256;d.DrawLine(InkDrawing.Pen(i%2==0?"#0BDBCDB4":"#1003060A",.45),new Point(x,y),new Point(x+2+random.NextDouble()*8,y+.6));}
  }
  var brushTile=new DrawingBrush(group){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,256,256),ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,256,256)};brushTile.Freeze();return brushTile;
 }
}

static class WindowFrame
{
 [StructLayout(LayoutKind.Sequential)]struct RectNative{public int Left,Top,Right,Bottom;}
 [DllImport("gdi32.dll")]static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int ellipseWidth,int ellipseHeight);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr value);
 [DllImport("user32.dll")]static extern int SetWindowRgn(IntPtr window,IntPtr region,bool redraw);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out RectNative rect);
 public static void Attach(Window window,FrameworkElement content,double radius)
 {
  void Update()
  {
   bool maximized=window.WindowState==WindowState.Maximized;double r=maximized?0:radius;
   content.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,content.ActualWidth),Math.Max(0,content.ActualHeight)),r,r);
   var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
   if(maximized){SetWindowRgn(handle,IntPtr.Zero,true);return;}
   if(!GetWindowRect(handle,out var bounds))return;
   double dpi=VisualTreeHelper.GetDpi(window).DpiScaleX;int diameter=(int)Math.Round(radius*2*dpi);
   var region=CreateRoundRectRgn(0,0,bounds.Right-bounds.Left+1,bounds.Bottom-bounds.Top+1,diameter,diameter);
   if(region!=IntPtr.Zero&&SetWindowRgn(handle,region,true)==0)DeleteObject(region);
  }
  window.SourceInitialized+=(_,_)=>Update();window.SizeChanged+=(_,_)=>Update();window.StateChanged+=(_,_)=>Update();content.SizeChanged+=(_,_)=>Update();
 }
}

static class BrandAssets
{
 static readonly Uri IconUri=new("pack://application:,,,/Assets/silk-quest.ico",UriKind.Absolute);
 public static ImageSource? WindowIcon()
 {
  try{return BitmapFrame.Create(IconUri,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);}catch(IOException){return null;}
 }
 public static System.Drawing.Icon TrayIcon()
 {
  try{using var resource=Application.GetResourceStream(IconUri)?.Stream;if(resource!=null){using var icon=new System.Drawing.Icon(resource,32,32);return (System.Drawing.Icon)icon.Clone();}}catch(IOException){}
  return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
 }
 public static void WriteIcon(string folder)
 {
  Directory.CreateDirectory(folder);int[] sizes={16,24,32,48,64,128,256};var images=new byte[sizes.Length][];
  for(int i=0;i<sizes.Length;i++)
  {
   int size=sizes[i];var visual=new DrawingVisual();using(var d=visual.RenderOpen())
   {
    d.PushTransform(new ScaleTransform(size/256d,size/256d));
    var bg=new LinearGradientBrush(UI.Color("#24333E"),UI.Color("#0A111A"),70);d.DrawRoundedRectangle(bg,InkDrawing.Pen("#837967",2),new Rect(12,12,232,232),43,43);
    d.DrawEllipse(null,InkDrawing.Pen("#43515B",8),new Point(128,129),77,77);
    InkDrawing.Path(d,"M135,52 C190,48 221,96 201,151 C185,195 125,221 82,187",InkDrawing.Pen("#C65B52",11));
    InkDrawing.Needle(d,new Point(80,194),new Point(166,67),4.8);
    InkDrawing.Path(d,"M166,67 C191,47 195,79 174,96 C146,118 173,128 164,149 C151,181 113,182 120,210",InkDrawing.Pen("#DC8170",3));
    d.Pop();
   }
   var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);images[i]=stream.ToArray();
   if(size==256)File.WriteAllBytes(Path.Combine(folder,"silk-quest.png"),images[i]);
  }
  using var file=File.Create(Path.Combine(folder,"silk-quest.ico"));using var writer=new BinaryWriter(file);writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
  for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
  foreach(var bytes in images)writer.Write(bytes);
 }
}
