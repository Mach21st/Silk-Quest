using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Windows.Automation;

namespace SilkJournal;

static class UI
{
 public static Brush Ink = B("#111923"), Paper = B("#E8DDCB"), Muted = B("#929CA5"), Line = B("#303A43"), Red = B("#C25B58");
 public static Color Color(string color) => (Color)ColorConverter.ConvertFromString(color);
 public static Brush B(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
 public static TextBlock Text(string text, double size=14, Brush? color=null, bool serif=true)
 {
  var label=new TextBlock { Text=text, FontSize=size, Foreground=color??Paper, FontFamily=new FontFamily(serif?(size>=26?"Georgia":"Cambria"):"Segoe UI"), VerticalAlignment=VerticalAlignment.Center, TextWrapping=TextWrapping.Wrap };
  TextOptions.SetTextRenderingMode(label,TextRenderingMode.Grayscale);TextOptions.SetTextFormattingMode(label,TextFormattingMode.Ideal);return label;
 }
 public static Button Button(string label, Action action, bool primary=false)
 {
  var b=new Button { Content=label, Margin=new Thickness(0,0,0,8) };
  AutomationProperties.SetName(b,label);
  if (primary) { b.Background=new LinearGradientBrush(Color("#A44843"),Color("#713238"),90); b.BorderBrush=B("#BE5B54"); }
  b.Click+=(_,_)=>action(); return b;
 }
 public static OrnamentRule Rule(double margin=18) => new OrnamentRule { Margin=new Thickness(0,margin,0,margin) };
 public static TextBlock Label(string s) => Text(s,11,Muted);
 public static TextBlock SettingsNote(string text)
 {
  var note=Text(text,14,Muted);note.LineHeight=21;note.Margin=new Thickness(0,0,0,8);return note;
 }
 public static ScrollViewer Scroll(FrameworkElement content,Thickness? margin=null)
 {
  var scroll=new ScrollViewer{Content=content,Margin=margin??new Thickness(0),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,CanContentScroll=false,HorizontalContentAlignment=HorizontalAlignment.Stretch};
  content.SetBinding(FrameworkElement.MaxWidthProperty,new Binding("ViewportWidth"){Source=scroll});return scroll;
 }
 public static ProgressBar Progress(double value) => new ProgressBar { Value=value*100, Maximum=100, Height=4, Foreground=Paper, Background=Line, BorderThickness=new Thickness(0), Margin=new Thickness(0,10,0,8) };
 public static void Fade(UIElement element, bool motion, double duration=.25)
 {
  if (!motion) return;
  element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.35,1,TimeSpan.FromSeconds(duration)));
 }
}

sealed class SilkArt : FrameworkElement
{
 protected override void OnRender(DrawingContext dc)
 {
  double w=ActualWidth, h=ActualHeight;
  if(w<5||h<5) return;
  var ivory=new Pen(UI.B("#51605F"),.7); var red=new Pen(UI.B("#7C4245"),1);
  void Curve(double x,double y,double a,double b,double c,double d,double e,double f,Pen p)
  {
   var geo=new StreamGeometry(); using(var ctx=geo.Open()) {ctx.BeginFigure(new Point(x,y),false,false);ctx.BezierTo(new Point(a,b),new Point(c,d),new Point(e,f),true,false);} dc.DrawGeometry(null,p,geo);
  }
  Curve(w*.15,h,w*.1,h*.55,w*.9,h*.9,w*.8,h*.25,ivory);
  Curve(w*.2,h,w*.8,h*.4,w*.3,h*.8,w,h*.1,ivory);
  Curve(w*.5,h,w*.4,h*.5,w*.95,h*.7,w*.85,h*.4,red);
  dc.DrawLine(new Pen(UI.B("#9D9B8E"),2),new Point(w*.35,h*.86),new Point(w*.81,h*.28));
  dc.DrawEllipse(null,ivory,new Point(w*.79,h*.30),3,7);
 }
}
