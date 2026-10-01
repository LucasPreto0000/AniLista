using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace AniLista {
public static class Theme {
 public static readonly Color Background=Color.FromArgb(16,17,25),Sidebar=Color.FromArgb(21,22,32),Surface=Color.FromArgb(28,30,43),Border=Color.FromArgb(46,48,65),Accent=Color.FromArgb(153,118,255),Text=Color.FromArgb(240,241,248),Muted=Color.FromArgb(156,161,182);
 public static readonly Color Input=Color.FromArgb(36,38,53),Accent2=Color.FromArgb(120,92,230),Success=Color.FromArgb(94,214,160),Warn=Color.FromArgb(255,196,107),Danger=Color.FromArgb(255,118,138);
 public static readonly Color Row=Color.FromArgb(35,37,53),Selected=Color.FromArgb(53,42,80),SelectedText=Color.FromArgb(203,180,255),Locked=Color.FromArgb(27,28,40),Soft=Color.FromArgb(200,203,220);
 // Métricas pertencem à janela, e não ao monitor principal do computador.
 public static float ScaleFor(Control control){
  AnimeCard card=null;
  for(Control current=control;current!=null;current=current.Parent){var form=current as DpiForm;if(form!=null)return form.LayoutScale;if(current is AnimeCard)card=(AnimeCard)current;}
 if(card!=null)return card.InitialScale;
  if(control!=null&&control.IsHandleCreated)return DpiForm.HandleScale(control.Handle);
  return 1f;
 }
 public static int S(Control owner,int value){return (int)Math.Round(value*ScaleFor(owner));}
 public static Size S(Control owner,int width,int height){return new Size(S(owner,width),S(owner,height));}
 public static void Place(Control owner,Control c,int x,int y,int w,int h){c.SetBounds(S(owner,x),S(owner,y),S(owner,w),S(owner,h));}
 static readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
 public static Font Font(Control owner,float size,FontStyle style=FontStyle.Regular){
  float pixels=size*96f/72f*ScaleFor(owner);string key=pixels.ToString(CultureInfo.InvariantCulture)+"|"+(int)style;Font f;
  if(!fonts.TryGetValue(key,out f)){f=new System.Drawing.Font("Segoe UI",pixels,style,GraphicsUnit.Pixel);fonts[key]=f;}return f;
 }
 public static Label Label(Control owner,string text,float size,Color color,FontStyle style=FontStyle.Regular){return new Label{Text=text,ForeColor=color,Font=Font(owner,size,style),AutoSize=false,BackColor=Color.Transparent,UseMnemonic=false};}
 static Icon appIcon;static bool iconLoaded;
 public static Icon AppIcon{get{
  if(!iconLoaded){iconLoaded=true;try{string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"AniLista.ico");appIcon=File.Exists(file)?new Icon(file):Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}}
  return appIcon;
 }}
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
 [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr handle,string app,string list);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,string lParam);
 static void OnHandle(Control control,Action<Control> action){EventHandler run=delegate{try{action(control);}catch{}};control.HandleCreated+=run;if(control.IsHandleCreated)run(control,EventArgs.Empty);}
 static int ColorRef(Color c){return c.R|(c.G<<8)|(c.B<<16);}
 public static void DarkTitle(Form form){OnHandle(form,c=>{
  int dark=1;if(DwmSetWindowAttribute(c.Handle,20,ref dark,4)!=0)DwmSetWindowAttribute(c.Handle,19,ref dark,4);
  int caption=ColorRef(Background),text=ColorRef(Text),border=ColorRef(Border);
  DwmSetWindowAttribute(c.Handle,35,ref caption,4);DwmSetWindowAttribute(c.Handle,36,ref text,4);DwmSetWindowAttribute(c.Handle,34,ref border,4);
 });}
 public static void DarkScroll(Control control){OnHandle(control,c=>SetWindowTheme(c.Handle,"DarkMode_Explorer",null));}
 public static void Cue(TextBox box,string text){OnHandle(box,c=>SendMessage(c.Handle,0x1501,(IntPtr)1,text));}
 public static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=Math.Max(2,Math.Min(radius*2,Math.Min(r.Width,r.Height)));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 public static Color Mix(Color a,Color b,float t){return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
 public static Color StatusColor(string s){return s=="watching"?Accent:s=="completed"?Success:Warn;}
 public static RoundButton Button(Control owner,string text,bool primary=false){
  return new RoundButton{Text=text,Primary=primary,Font=Font(owner,10,FontStyle.Bold),Height=S(owner,40),Cursor=Cursors.Hand,BackColor=primary?Accent:Surface,ForeColor=primary?Color.FromArgb(15,12,25):Text};
 }
 public static TextBox TextBox(Control owner){return new TextBox{BackColor=Input,ForeColor=Text,BorderStyle=BorderStyle.None,Font=Font(owner,11)};}
 public static InputBox Wrap(Control owner,TextBox t,int x,int y,int w,int h,AnchorStyles anchor=AnchorStyles.Top|AnchorStyles.Left,bool icon=false){
  var box=new InputBox{Anchor=anchor,Icon=icon};Place(owner,box,x,y,w,h);
  int left=S(owner,icon?40:13);t.Location=new Point(left,(box.Height-t.PreferredHeight)/2);t.Width=box.Width-left-S(owner,13);t.Anchor=AnchorStyles.Left|AnchorStyles.Right;
  t.Enter+=delegate{box.Active=true;box.Invalidate();};t.Leave+=delegate{box.Active=false;box.Invalidate();};
  box.Controls.Add(t);return box;
 }
 public static string Initial(string title){
  title=(title??"").Trim();if(title.Length==0)return "?";
  return StringInfo.GetNextTextElement(title).ToUpper();
 }
 public static string StatusName(string s){return s=="watching"?"Assistindo":s=="completed"?"Concluídos":"Quero assistir";}
 public static string Episodes(Anime a){
  if(a.Status=="watching")return "Episódio "+a.Episode+(a.Total>0?" de "+a.Total:" · total não informado");
  if(a.Status=="completed")return a.Total>0?a.Total+" episódios concluídos":"Anime concluído";
  return a.Total>0?a.Total+" episódios":"Total de episódios não informado";
 }
 public const TextFormatFlags Line=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding;
 public const TextFormatFlags Center=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding;
 // Desenha a capa (ou uma capa provisória com a inicial) recortada com cantos arredondados.
 public static void DrawCover(Control owner,Graphics g,Rectangle r,Image image,string initial,int radius){
  if(r.Width<=2||r.Height<=2)return;
  using(var path=Round(r,radius)){
   GraphicsState state=g.Save();g.SetClip(path,CombineMode.Intersect);
   if(image!=null&&image.Width>0&&image.Height>0){
    float s=Math.Max((float)r.Width/image.Width,(float)r.Height/image.Height);int w=(int)Math.Ceiling(image.Width*s),h=(int)Math.Ceiling(image.Height*s);
    g.DrawImage(image,r.X+(r.Width-w)/2,r.Y+(r.Height-h)/2,w,h);
   }else{
    using(var b=new LinearGradientBrush(r,Color.FromArgb(84,62,148),Color.FromArgb(36,30,62),55f))g.FillRectangle(b,r);
    TextRenderer.DrawText(g,initial,Font(owner,Math.Max(8,(int)Math.Round(r.Height/ScaleFor(owner)*0.24f)),FontStyle.Bold),r,Color.FromArgb(210,190,255),Center);
   }
   g.Restore(state);
   using(var pen=new Pen(Color.FromArgb(38,255,255,255)))g.DrawPath(pen,path);
  }
 }
 public static void Chip(Control owner,Graphics g,string text,Color color,Rectangle area,bool alignRight,out Rectangle chip){
  Font f=Font(owner,8,FontStyle.Bold);Size s=TextRenderer.MeasureText(g,text,f,new Size(400,40),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
  int w=s.Width+S(owner,18);chip=new Rectangle(alignRight?area.Right-w:area.X,area.Y,w,area.Height);
  using(var p=Round(chip,chip.Height/2))using(var b=new SolidBrush(Color.FromArgb(46,color)))g.FillPath(b,p);
  TextRenderer.DrawText(g,text,f,chip,color,Center);
 }
}
}
