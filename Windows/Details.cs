using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Collections.Generic;

namespace SilkJournal;

// These ornaments are resolution independent drawings, not a screenshot of the design.
static class InkDrawing
{
 public static Pen Pen(string c,double width=1) => new Pen(UI.B(c),width) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round };
 public static void Path(DrawingContext d,string data,Pen pen,Brush? fill=null) => d.DrawGeometry(fill,pen,Geometry.Parse(data));
 public static void Flower(DrawingContext d,Point c,double size,Pen pen)
 {
  d.PushTransform(new TranslateTransform(c.X,c.Y));d.PushTransform(new ScaleTransform(size/10,size/10));
  Path(d,"M0,-9 C3,-5 3,-2 0,0 C-3,-2 -3,-5 0,-9 M9,0 C5,3 2,3 0,0 C2,-3 5,-3 9,0 M0,9 C-3,5 -3,2 0,0 C3,2 3,5 0,9 M-9,0 C-5,-3 -2,-3 0,0 C-2,3 -5,3 -9,0",pen);
  d.Pop();d.Pop();
 }
 public static void Needle(DrawingContext d,Point tip,Point eye,double thickness=2)
 {
  Vector v=eye-tip;v.Normalize();var normal=new Vector(-v.Y,v.X);Point near=tip+(eye-tip)*.82;
  var g=new StreamGeometry();using(var ctx=g.Open()){ctx.BeginFigure(tip,true,true);ctx.LineTo(near+normal*thickness,true,false);ctx.BezierTo(eye+normal*thickness*1.4,eye+v*10+normal*thickness*1.5,eye+v*11,true,false);ctx.BezierTo(eye+v*10-normal*thickness*1.5,eye-normal*thickness*1.4,near-normal*thickness,true,false);}
  var metal=new LinearGradientBrush{StartPoint=new Point(.2,0),EndPoint=new Point(.8,1)};metal.GradientStops.Add(new GradientStop(UI.Color("#5D645F"),0));metal.GradientStops.Add(new GradientStop(UI.Color("#CFBEA5"),.26));metal.GradientStops.Add(new GradientStop(UI.Color("#F7E5C9"),.43));metal.GradientStops.Add(new GradientStop(UI.Color("#968A77"),.61));metal.GradientStops.Add(new GradientStop(UI.Color("#D2BE9B"),1));
  d.DrawGeometry(metal,Pen("#A39782",.4),g);
  d.DrawLine(Pen("#F3E3C9",.65),tip+normal*.25,eye+normal*thickness*.65);
  d.DrawLine(Pen("#746F65",.45),tip-normal*.3,near-normal*thickness*.7);
  d.PushTransform(new RotateTransform(Math.Atan2(v.Y,v.X)*180/Math.PI+90,eye.X,eye.Y));
  d.DrawRoundedRectangle(UI.Ink,Pen("#E4CCAA",.7),new Rect(eye.X-Math.Max(.8,thickness*.48),eye.Y-3.5,Math.Max(1.6,thickness*.96),8),Math.Max(.7,thickness*.45),1.5);d.Pop();
 }
}

sealed class OrnamentRule : FrameworkElement
{
 public OrnamentRule(){Height=18;IsHitTestVisible=false;}
 protected override void OnRender(DrawingContext d)
 {
  double w=ActualWidth,y=ActualHeight/2;
  var b=new LinearGradientBrush();b.StartPoint=new Point(0,0);b.EndPoint=new Point(1,0);b.GradientStops.Add(new GradientStop(UI.Color("#1F2B32"),0));b.GradientStops.Add(new GradientStop(UI.Color("#746E61"),.5));b.GradientStops.Add(new GradientStop(UI.Color("#1F2B32"),1));
  var pen=new Pen(b,.7);d.DrawLine(pen,new Point(6,y),new Point(w/2-11,y));d.DrawLine(pen,new Point(w/2+11,y),new Point(w-6,y));
  InkDrawing.Flower(d,new Point(w/2,y),7,InkDrawing.Pen("#8A8070",.7));
  d.DrawEllipse(UI.B("#7B7667"),null,new Point(3,y),1.5,.8);d.DrawEllipse(UI.B("#7B7667"),null,new Point(w-3,y),1.5,.8);
 }
}

sealed class PanelSpine : FrameworkElement
{
 public PanelSpine(){IsHitTestVisible=false;}
 protected override void OnRender(DrawingContext d)
 {
  double x=ActualWidth/2,h=ActualHeight;var brush=new LinearGradientBrush();brush.StartPoint=new Point(0,0);brush.EndPoint=new Point(0,1);brush.GradientStops.Add(new GradientStop(UI.Color("#7B7768"),0));brush.GradientStops.Add(new GradientStop(UI.Color("#34404A"),.25));brush.GradientStops.Add(new GradientStop(UI.Color("#34404A"),.8));brush.GradientStops.Add(new GradientStop(UI.Color("#6A6A5C"),1));d.DrawLine(new Pen(brush,.6),new Point(x,4),new Point(x,h-4));d.DrawEllipse(UI.B("#747465"),null,new Point(x,4),1.2,2.1);d.DrawEllipse(UI.B("#747465"),null,new Point(x,h-4),1.2,2.1);
 }
}

sealed class JournalSurface : FrameworkElement
{
 readonly bool foliage;
 public JournalSurface(bool foliage=false){this.foliage=foliage;IsHitTestVisible=false;}
 protected override void OnRender(DrawingContext d)
 {
  double w=ActualWidth,h=ActualHeight;if(w<1||h<1)return;
  var bg=new LinearGradientBrush();bg.StartPoint=new Point(0,0);bg.EndPoint=new Point(1,1);bg.GradientStops.Add(new GradientStop(UI.Color("#121B23"),0));bg.GradientStops.Add(new GradientStop(UI.Color("#0D141B"),.58));bg.GradientStops.Add(new GradientStop(UI.Color("#111C24"),1));d.DrawRectangle(bg,null,new Rect(0,0,w,h));
  void Mist(double x,double y,double rx,double ry,string color)
  {
   var glow=new RadialGradientBrush();glow.Center=glow.GradientOrigin=new Point(x,y);glow.RadiusX=rx;glow.RadiusY=ry;glow.GradientStops.Add(new GradientStop(UI.Color(color),0));glow.GradientStops.Add(new GradientStop(Colors.Transparent,1));d.DrawRectangle(glow,null,new Rect(0,0,w,h));
  }
  Mist(.29,.14,.45,.33,"#204A5E68");Mist(.7,.63,.62,.38,"#0E607278");Mist(.91,.94,.25,.3,"#1E4B4E48");Mist(.49,.47,.25,.65,"#0B9D9382");
  d.DrawRectangle(Grain,null,new Rect(0,0,w,h));
  if(!foliage)return;
  DrawBotanical(d,w,h);
 }
 static Point Bezier(Point a,Point b,Point c,Point e,double t)
 {double u=1-t;return new Point(u*u*u*a.X+3*u*u*t*b.X+3*u*t*t*c.X+t*t*t*e.X,u*u*u*a.Y+3*u*u*t*b.Y+3*u*t*t*c.Y+t*t*t*e.Y);}
 static void DrawBotanical(DrawingContext d,double w,double h)
 {
  var random=new Random(301);d.PushOpacity(.34);
  void Stem(Point start,Point end,double bow,int leaves)
  {
   var c1=new Point(start.X+bow,start.Y-(start.Y-end.Y)*.35);var c2=new Point(end.X-bow*.6,end.Y+(start.Y-end.Y)*.3);
   var stem=new StreamGeometry();using(var ctx=stem.Open()){ctx.BeginFigure(start,false,false);ctx.BezierTo(c1,c2,end,true,false);}d.DrawGeometry(null,InkDrawing.Pen("#435865",.7),stem);
   for(int i=0;i<leaves;i++)
   {
    double t=.09+i*.86/Math.Max(1,leaves-1);Point root=Bezier(start,c1,c2,end,t);double side=i%2==0?-1:1;
    Point tip=root+new Vector(side*(20+random.NextDouble()*31),-(42+random.NextDouble()*41));Vector v=tip-root;Vector n=new Vector(-v.Y,v.X);n.Normalize();double width=7+random.NextDouble()*9;
    var leaf=new StreamGeometry();using(var ctx=leaf.Open())
    {
     ctx.BeginFigure(root,true,true);ctx.BezierTo(root+v*.22+n*width*.5,root+v*.36+n*width*1.1,root+v*.57+n*width*.7,true,false);
     ctx.BezierTo(root+v*.71+n*width*.85,root+v*.92+n*width*.2,tip,true,false);
     ctx.BezierTo(root+v*.82-n*width*.4,root+v*.72-n*width,root+v*.52-n*width*.78,true,false);
     ctx.BezierTo(root+v*.32-n*width*.75,root+v*.15-n*width*.2,root,true,false);
    }
    var fill=new LinearGradientBrush(UI.Color(i%3==0?"#253C4C":"#203442"),UI.Color("#12232E"),side<0?20:160);d.DrawGeometry(fill,InkDrawing.Pen("#344E60",.38),leaf);
    var vein=new StreamGeometry();using(var ctx=vein.Open()){ctx.BeginFigure(root,false,false);ctx.QuadraticBezierTo(root+v*.5+n*1.5,tip,true,false);}d.DrawGeometry(null,InkDrawing.Pen("#536876",.4),vein);
    for(int k=1;k<=4;k++){double f=.14+k*.13;Point center=root+v*f;d.DrawLine(InkDrawing.Pen("#3C5362",.3),center,center+v*.13+n*(width*.55*(k%2==0?1:-1)));}
   }
  }
  // A rooted cluster with connected, broad, irregular leaves, like the reference.
  Stem(new Point(w*.52,h+18),new Point(w*.29,h*.37),-w*.18,10);
  Stem(new Point(w*.55,h+18),new Point(w*.57,h*.42),w*.13,9);
  Stem(new Point(w*.49,h*.92),new Point(w*.13,h*.57),-w*.12,6);
  Stem(new Point(w*.55,h*.94),new Point(w*.77,h*.59),w*.13,6);
  Stem(new Point(w*.5,h*.8),new Point(w*.4,h*.49),-w*.09,5);
  d.Pop();
 }
 static readonly DrawingBrush Grain=MakeGrain();
 static DrawingBrush MakeGrain()
 {
  var group=new DrawingGroup();var random=new Random(371);using(var context=group.Open())for(int i=0;i<560;i++){byte alpha=(byte)random.Next(2,12);var brush=new SolidColorBrush(Color.FromArgb(alpha,210,205,185));context.DrawEllipse(brush,null,new Point(random.NextDouble()*128,random.NextDouble()*128),.25+random.NextDouble()*.35,.25+random.NextDouble()*.35);}
  var result=new DrawingBrush(group){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,128,128),ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,128,128)};result.Freeze();return result;
 }
}

sealed class JournalIcon : FrameworkElement
{
 readonly string kind;readonly bool active;
 public JournalIcon(string kind,bool active=false,double size=25){this.kind=kind;this.active=active;Width=Height=size;IsHitTestVisible=false;}
 protected override void OnRender(DrawingContext d)
 {
  d.PushTransform(new ScaleTransform(ActualWidth/28,ActualHeight/28));var p=InkDrawing.Pen(active?"#D86A60":"#C5BFB1",1);
  switch(kind)
  {
   case "Сегодня":case "project":
    d.DrawEllipse(null,p,new Point(14,14),10,10);d.DrawEllipse(null,p,new Point(14,14),6,6);
    InkDrawing.Path(d,"M14,0 L17,11 28,14 17,17 14,28 11,17 0,14 11,11Z",p);break;
   case "Все квесты":InkDrawing.Path(d,"M5,3 L23,3 23,26 14,19 5,26Z M6,4 L14,10 22,4",p);break;
   case "Путешествие":InkDrawing.Path(d,"M2,7 L9,2 19,7 26,2 26,22 19,27 9,22 2,27Z M9,2 L9,22 M19,7 L19,27",p);break;
   case "Завершённые":d.DrawEllipse(null,p,new Point(14,14),11,11);InkDrawing.Path(d,"M8,14 L12,18 20,9",p);break;
   case "Лавка":InkDrawing.Path(d,"M4,11 L24,11 22,26 6,26Z M9,11 L9,7 C9,0 19,0 19,7 L19,11 M5,16 L23,16",p);break;
   case "pin":InkDrawing.Path(d,"M7,3 L21,3 19,7 19,12 23,16 15,16 14,27 13,16 5,16 9,12 9,7Z",p);break;
   case "Настройки":d.DrawEllipse(null,p,new Point(14,14),8,8);d.DrawEllipse(null,p,new Point(14,14),3,3);for(int i=0;i<8;i++){double a=i*Math.PI/4;d.DrawLine(p,new Point(14+Math.Cos(a)*9,14+Math.Sin(a)*9),new Point(14+Math.Cos(a)*12,14+Math.Sin(a)*12));}break;
   default:InkDrawing.Flower(d,new Point(14,14),13,p);break;
  }d.Pop();
 }
}

sealed class QuestSeal : FrameworkElement
{
 readonly bool done,active;
 public QuestSeal(bool done,bool active=false){this.done=done;this.active=active;Width=Height=31;IsHitTestVisible=false;}
 protected override void OnRender(DrawingContext d)
 {
  var c=new Point(15.5,15.5);
  if(done)
  {
   var fill=new LinearGradientBrush(UI.Color("#E5D8C4"),UI.Color("#AB9E8D"),80);d.DrawEllipse(fill,InkDrawing.Pen("#B4AA9D",.7),c,12,12);InkDrawing.Path(d,"M9,15 L14,20 22,10",InkDrawing.Pen("#172029",1.9));
  }
  else
  {
   d.DrawEllipse(UI.B(active?"#352228":"#0D151D"),InkDrawing.Pen(active?"#CA5955":"#B9B6AE",active?2:1),c,12,12);
   if(active)InkDrawing.Path(d,"M7,22 C2,12 7,3 16,3",InkDrawing.Pen("#EF8170",2.7));
  }
 }
}

sealed class SilkThread : FrameworkElement
{
 readonly List<(FrameworkElement Row,bool Done)> anchors=new();
 public SilkThread(){IsHitTestVisible=false;}
 public void Watch(FrameworkElement row,bool done){anchors.Add((row,done));row.SizeChanged+=(_,_)=>InvalidateVisual();}
 protected override void OnRender(DrawingContext d)
 {
  if(anchors.Count==0||ActualHeight<1)return;var points=new List<Point>();
  foreach(var anchor in anchors){if(!anchor.Row.IsArrangeValid)return;var p=anchor.Row.TranslatePoint(new Point(0,anchor.Row.ActualHeight/2),this);points.Add(new Point(12+Math.Sin(points.Count*1.3)*2.3,p.Y));}
  var trunk=new StreamGeometry();using(var ctx=trunk.Open()){ctx.BeginFigure(new Point(12,-12),false,false);Point last=new Point(12,-12);for(int i=0;i<points.Count;i++){var p=points[i];double span=p.Y-last.Y;ctx.BezierTo(new Point(last.X-2.7,last.Y+span*.32),new Point(p.X+2.1,p.Y-span*.28),p,true,false);last=p;}}
  d.DrawGeometry(null,InkDrawing.Pen("#A99F8B",.85),trunk);
  for(int i=0;i<points.Count;i++)
  {
   var p=points[i];double lift=12+(i%3)*3;var branch=new StreamGeometry();using(var ctx=branch.Open()){ctx.BeginFigure(new Point(p.X,p.Y-lift),false,false);ctx.BezierTo(new Point(p.X-1,p.Y+3+i%2),new Point(26,p.Y+2-Math.Sin(i)),new Point(42,p.Y),true,false);}d.DrawGeometry(null,InkDrawing.Pen(anchors[i].Done?"#D7C9AC":"#A89D87",.68+(i%3)*.13),branch);
   if(i%2==0){var fiber=new StreamGeometry();using(var ctx=fiber.Open()){ctx.BeginFigure(new Point(p.X+1,p.Y-lift-4),false,false);ctx.BezierTo(new Point(p.X+3,p.Y-3),new Point(23,p.Y+4),new Point(36,p.Y+.7),true,false);}d.DrawGeometry(null,InkDrawing.Pen("#716D60",.38),fiber);}
  }
 }
}

sealed class SilkCompass : FrameworkElement
{
 public static readonly DependencyProperty ProgressProperty=DependencyProperty.Register(nameof(Progress),typeof(double),typeof(SilkCompass),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Progress{get=>(double)GetValue(ProgressProperty);set=>SetValue(ProgressProperty,value);}
 public SilkCompass(){Width=Height=148;IsHitTestVisible=false;}
 public void SetProgress(double value,bool animate){double from=Progress;BeginAnimation(ProgressProperty,null);Progress=value;if(animate)BeginAnimation(ProgressProperty,new DoubleAnimation(from,value,TimeSpan.FromMilliseconds(650)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});}
 protected override void OnRender(DrawingContext d)
 {
  var center=new Point(74,74);double r=56;d.DrawEllipse(null,InkDrawing.Pen("#29343E",5),center,r,r);
  double fraction=Math.Clamp(Progress,0,1);if(fraction>.001)
  {
   double a=(-90+Math.Min(.9999,fraction)*360)*Math.PI/180;var g=new StreamGeometry();using(var ctx=g.Open()){ctx.BeginFigure(new Point(74,18),false,false);ctx.ArcTo(new Point(74+Math.Cos(a)*r,74+Math.Sin(a)*r),new Size(r,r),0,fraction>.5,SweepDirection.Clockwise,true,false);}d.DrawGeometry(null,InkDrawing.Pen("#CC5553",5),g);
  }
  InkDrawing.Needle(d,new Point(50,106),new Point(86,46),1.25);
  InkDrawing.Path(d,"M86,46 C101,43 90,57 86,62 C74,81 102,86 105,102 C111,127 84,130 67,127",InkDrawing.Pen("#9C4549",1));
 }
}

sealed class NeedleCorner : FrameworkElement
{
 public NeedleCorner(){Height=210;IsHitTestVisible=false;}
 protected override void OnRender(DrawingContext d)
 {
  double w=ActualWidth,h=ActualHeight;if(w<1)return;
  d.PushTransform(new ScaleTransform(w/390,h/210));
  var faint=InkDrawing.Pen("#52534C",.55);
  foreach(var data in new[]{"M45,207 C99,147 206,197 307,117 C351,86 372,81 389,24","M80,210 C180,171 224,169 302,152 C345,145 392,121 389,65","M143,210 C214,175 270,204 338,166 C380,142 366,101 390,94","M260,210 C315,172 385,189 386,131 M330,210 C357,190 379,207 389,177","M389,208 L352,208 C372,202 380,186 387,166 L389,208"})InkDrawing.Path(d,data,faint);
  string thread="M312,80 C329,55 343,72 327,89 C309,109 288,99 292,118 C297,144 342,145 329,162 C318,176 255,166 218,180 C177,195 197,208 231,202 C267,196 241,180 207,185 C165,191 129,207 85,201";
  InkDrawing.Path(d,thread,InkDrawing.Pen("#2A1B23",2.7));
  InkDrawing.Path(d,thread,InkDrawing.Pen("#AD4E47",1.05));
  InkDrawing.Path(d,"M312,80 C335,86 308,113 323,129 C345,153 298,169 278,179 C254,191 290,206 306,199",InkDrawing.Pen("#753D40",.7));
  InkDrawing.Path(d,"M217,180 C197,185 187,193 199,200",InkDrawing.Pen("#D17961",.45));
  InkDrawing.Needle(d,new Point(115,188),new Point(312,80),3.0);
  InkDrawing.Path(d,"M389,210 L389,177 M357,210 L389,210",InkDrawing.Pen("#A08C70",.7));d.Pop();
 }
}

sealed class SpoolIcon : FrameworkElement
{
 public static readonly DependencyProperty AngleProperty=DependencyProperty.Register(nameof(Angle),typeof(double),typeof(SpoolIcon),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Angle{get=>(double)GetValue(AngleProperty);set=>SetValue(AngleProperty,value);}
 public static readonly DependencyProperty SecondsProperty=DependencyProperty.Register(nameof(Seconds),typeof(double),typeof(SpoolIcon),new FrameworkPropertyMetadata(1500d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Seconds{get=>(double)GetValue(SecondsProperty);set=>SetValue(SecondsProperty,value);}
 double totalSeconds=1500;bool timeInitialized;
 internal double Winding=>Math.Clamp((Seconds-5)/Math.Max(1,totalSeconds-5),0,1);
 internal double LooseThread=>Math.Clamp(Seconds/5,0,1);
 public bool IsSpinning{get;private set;}
 bool requestedRunning,requestedMotion;
 public SpoolIcon(){Width=94;Height=54;IsHitTestVisible=false;IsVisibleChanged+=(_,_)=>UpdateSpin();Unloaded+=(_,_)=>SetRunning(false,false);}
 public void SetRunning(bool running,bool motion)
 {
  requestedRunning=running;requestedMotion=motion;UpdateSpin();
 }
 public void SetTime(double remaining,double total,bool motion)
 {
  remaining=Math.Clamp(remaining,0,Math.Max(1,total));total=Math.Max(1,total);
  if(timeInitialized&&Math.Abs(totalSeconds-total)<.001&&Math.Abs((double)GetAnimationBaseValue(SecondsProperty)-remaining)<.001)return;
  double from=Seconds;BeginAnimation(SecondsProperty,null);Seconds=remaining;totalSeconds=total;
  if(timeInitialized&&motion&&IsVisible&&remaining<from&&from-remaining<=2)
   BeginAnimation(SecondsProperty,new DoubleAnimation(from,remaining,TimeSpan.FromMilliseconds(800)));
  timeInitialized=true;InvalidateVisual();
 }
 void UpdateSpin()
 {
  bool spin=requestedRunning&&requestedMotion&&IsVisible;if(spin==IsSpinning)return;
  double angle=Angle%360;BeginAnimation(AngleProperty,null);Angle=angle;IsSpinning=spin;
  if(spin)BeginAnimation(AngleProperty,new DoubleAnimation(angle,angle+360,TimeSpan.FromSeconds(4)){RepeatBehavior=RepeatBehavior.Forever});
 }
 protected override void OnRender(DrawingContext d)
 {
  double phase=Angle*Math.PI/180;
  var rim=InkDrawing.Pen("#C5AE90",.85);
  d.DrawEllipse(UI.B("#B5A187"),rim,new Point(12,27),5,14);
  var body=new LinearGradientBrush();body.StartPoint=new Point(0,0);body.EndPoint=new Point(0,1);body.GradientStops.Add(new GradientStop(UI.Color("#55272B"),0));body.GradientStops.Add(new GradientStop(UI.Color("#BF6158"),.38));body.GradientStops.Add(new GradientStop(UI.Color("#6F3035"),1));
  // The spindle remains after the red winding has been paid out.
  var core=new LinearGradientBrush(UI.Color("#615846"),UI.Color("#B4A084"),90);
  d.DrawRoundedRectangle(core,InkDrawing.Pen("#88785F",.6),new Rect(12,22,36,10),2,2);
  double winding=Winding,bandWidth=36*winding,bandHeight=10+10*winding;
  if(winding>0)
  {
  var band=new Rect(48-bandWidth,27-bandHeight/2,bandWidth,bandHeight);
  d.DrawRoundedRectangle(body,null,band,Math.Min(2,bandWidth/2),2);
  d.PushClip(new RectangleGeometry(band));
  double travel=(Angle/360%1)*6;
  for(int i=-5;i<13;i++)
  {
   double x=12+i*6+travel;
   d.DrawLine(InkDrawing.Pen("#612A30",1.5),new Point(x,17),new Point(x-9,37));
   d.DrawLine(InkDrawing.Pen("#D27A69",.65),new Point(x+1.3,17),new Point(x-7.7,37));
  }
  d.Pop();
  }
  d.DrawEllipse(UI.B("#C5B092"),rim,new Point(49,27),5,14);
  d.DrawEllipse(null,InkDrawing.Pen("#796754",.8),new Point(49.5,27),3.1,11);
  d.DrawEllipse(UI.Ink,null,new Point(50,27),1.5,3);
  var loose=new StreamGeometry();using(var ctx=loose.Open())
  {
   ctx.BeginFigure(new Point(43,35),false,false);
   ctx.BezierTo(new Point(56,32+Math.Sin(phase)*3),new Point(53,44+Math.Sin(phase+1)*3),new Point(66,41+Math.Sin(phase)*2),true,false);
   ctx.BezierTo(new Point(77,38+Math.Sin(phase+1.4)*4),new Point(77,48+Math.Sin(phase+.5)*3),new Point(91,44+Math.Sin(phase)*2),true,false);
  }
  double looseAmount=LooseThread;
  if(looseAmount>0)
  {
   // Consume the free strand from its left end towards its right-hand tip.
   d.PushClip(new RectangleGeometry(new Rect(42+(1-looseAmount)*50,0,50*looseAmount,54)));
   d.DrawGeometry(null,InkDrawing.Pen("#B75851",1.1),loose);d.Pop();
  }
 }
}

sealed class RosaryIcon : FrameworkElement
{
 public RosaryIcon(){Width=28;Height=32;IsHitTestVisible=false;}
 protected override void OnRender(DrawingContext d)
 {
  d.DrawEllipse(null,InkDrawing.Pen("#918370",.7),new Point(14,14),9,11);
  for(int i=0;i<9;i++)
  {
   double a=(i*36-72)*Math.PI/180;var p=new Point(14+Math.Cos(a)*9,14+Math.Sin(a)*11);
   d.DrawEllipse(UI.B(i%2==0?"#D5CCB5":"#A9B3AE"),InkDrawing.Pen("#737F7C",.5),p,2.7,3.1);
   d.DrawEllipse(UI.B("#ECE4CD"),null,new Point(p.X-.7,p.Y-.9),.6,.8);
  }
  InkDrawing.Path(d,"M13,25 L12,30 M15,25 L17,29",InkDrawing.Pen("#B45B51",1));
 }
}
