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
[DataContract] public sealed class Anime {
 [DataMember] public string Id=Guid.NewGuid().ToString("N");
 [DataMember] public int CatalogId;
 [DataMember] public string Title="", Cover="", Status="planned";
 [DataMember] public int Episode, Total, Year;
 [DataMember] public string Updated=DateTime.UtcNow.ToString("o");
 public Anime Copy(){return (Anime)MemberwiseClone();}
 public void Validate(){
  Title=(Title??"").Trim();
  if(Title.Length==0||Title.Length>180)throw new Exception("Informe um título com até 180 caracteres.");
  if(Status!="planned"&&Status!="watching"&&Status!="completed")throw new Exception("Selecione uma lista válida.");
  if(Episode<0||Total<0||Total>100000||Episode>100000)throw new Exception("Confira os números de episódios.");
  if(Status=="planned")Episode=0;
  if(Status=="completed"&&Total>0)Episode=Total;
  if(Total>0&&Episode>Total)throw new Exception("O episódio atual não pode ser maior que o total.");
  if(String.IsNullOrEmpty(Id))Id=Guid.NewGuid().ToString("N");
  Updated=DateTime.UtcNow.ToString("o");
 }
}
[DataContract] public sealed class Library {
 [DataMember] public int Version=1;
 [DataMember] public List<Anime> Animes=new List<Anime>();
}
public sealed class LibraryStore {
 public readonly string Folder,FilePath; public bool Recovered;
 public LibraryStore(string folder){Folder=folder;FilePath=Path.Combine(folder,"biblioteca.json");}
 Library Read(string path){using(var stream=File.OpenRead(path)){
  var library=(Library)new DataContractJsonSerializer(typeof(Library)).ReadObject(stream);
  if(library==null||library.Version!=1||library.Animes==null)throw new IOException("Formato da biblioteca inválido.");
  if(library.Animes.Any(a=>a==null||String.IsNullOrWhiteSpace(a.Title)||String.IsNullOrWhiteSpace(a.Id)))throw new IOException("Uma anotação está inválida.");
  return library;
 }}
 public List<Anime> Load(){
  Directory.CreateDirectory(Folder);if(!File.Exists(FilePath))return new List<Anime>();
  try{return Read(FilePath).Animes;}catch{
   string backup=FilePath+".bak";
   if(!File.Exists(backup))throw new IOException("Os arquivos da biblioteca foram preservados em "+Folder);
   Library recovered=Read(backup);
   File.Copy(FilePath,FilePath+".recuperado-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"),false);
   Recovered=true;return recovered.Animes;
  }
 }
 public void Save(List<Anime> items){
  Directory.CreateDirectory(Folder);string temporary=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{
   using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
    new DataContractJsonSerializer(typeof(Library)).WriteObject(stream,new Library{Animes=items});stream.Flush(true);
   }
   if(File.Exists(FilePath))File.Replace(temporary,FilePath,FilePath+".bak");else File.Move(temporary,FilePath);
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}
public static class Theme {
 public static readonly Color Background=Color.FromArgb(16,17,25),Sidebar=Color.FromArgb(21,22,32),Surface=Color.FromArgb(28,30,43),Border=Color.FromArgb(46,48,65),Accent=Color.FromArgb(153,118,255),Text=Color.FromArgb(240,241,248),Muted=Color.FromArgb(156,161,182);
 public static readonly Color Input=Color.FromArgb(36,38,53),Accent2=Color.FromArgb(120,92,230),Success=Color.FromArgb(94,214,160),Warn=Color.FromArgb(255,196,107),Danger=Color.FromArgb(255,118,138);
 public static readonly Color Row=Color.FromArgb(35,37,53),Selected=Color.FromArgb(53,42,80),SelectedText=Color.FromArgb(203,180,255),Locked=Color.FromArgb(27,28,40),Soft=Color.FromArgb(200,203,220);
 // Escala para monitores com zoom (125%, 150%...): todas as medidas são escritas em 96 DPI e passam por S().
 static float scale;
 public static float Scale{get{if(scale<=0){try{using(var g=Graphics.FromHwnd(IntPtr.Zero))scale=Math.Max(1f,g.DpiX/96f);}catch{scale=1f;}}return scale;}}
 public static int S(int value){return (int)Math.Round(value*Scale);}
 public static Size S(int width,int height){return new Size(S(width),S(height));}
 public static void Place(Control c,int x,int y,int w,int h){c.SetBounds(S(x),S(y),S(w),S(h));}
 static readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
 public static Font Font(float size,FontStyle style=FontStyle.Regular){
  string key=size.ToString(CultureInfo.InvariantCulture)+"|"+(int)style;Font f;
  if(!fonts.TryGetValue(key,out f)){f=new Font("Segoe UI",size,style);fonts[key]=f;}return f;
 }
 public static Label Label(string text,float size,Color color,FontStyle style=FontStyle.Regular){return new Label{Text=text,ForeColor=color,Font=Font(size,style),AutoSize=false,BackColor=Color.Transparent,UseMnemonic=false};}
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
 public static RoundButton Button(string text,bool primary=false){
  return new RoundButton{Text=text,Primary=primary,Font=Font(10,FontStyle.Bold),Height=S(40),Cursor=Cursors.Hand,BackColor=primary?Accent:Surface,ForeColor=primary?Color.FromArgb(15,12,25):Text};
 }
 public static TextBox TextBox(){return new TextBox{BackColor=Input,ForeColor=Text,BorderStyle=BorderStyle.None,Font=Font(11)};}
 public static InputBox Wrap(TextBox t,int x,int y,int w,int h,AnchorStyles anchor=AnchorStyles.Top|AnchorStyles.Left,bool icon=false){
  var box=new InputBox{Anchor=anchor,Icon=icon};Place(box,x,y,w,h);
  int left=S(icon?40:13);t.Location=new Point(left,(box.Height-t.PreferredHeight)/2);t.Width=box.Width-left-S(13);t.Anchor=AnchorStyles.Left|AnchorStyles.Right;
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
 public static void DrawCover(Graphics g,Rectangle r,Image image,string initial,int radius){
  if(r.Width<=2||r.Height<=2)return;
  using(var path=Round(r,radius)){
   GraphicsState state=g.Save();g.SetClip(path,CombineMode.Intersect);
   if(image!=null&&image.Width>0&&image.Height>0){
    float s=Math.Max((float)r.Width/image.Width,(float)r.Height/image.Height);int w=(int)Math.Ceiling(image.Width*s),h=(int)Math.Ceiling(image.Height*s);
    g.DrawImage(image,r.X+(r.Width-w)/2,r.Y+(r.Height-h)/2,w,h);
   }else{
    using(var b=new LinearGradientBrush(r,Color.FromArgb(84,62,148),Color.FromArgb(36,30,62),55f))g.FillRectangle(b,r);
    TextRenderer.DrawText(g,initial,Font(Math.Max(8,(int)Math.Round(r.Height/Scale*0.24f)),FontStyle.Bold),r,Color.FromArgb(210,190,255),Center);
   }
   g.Restore(state);
   using(var pen=new Pen(Color.FromArgb(38,255,255,255)))g.DrawPath(pen,path);
  }
 }
 public static void Chip(Graphics g,string text,Color color,Rectangle area,bool alignRight,out Rectangle chip){
  Font f=Font(8,FontStyle.Bold);Size s=TextRenderer.MeasureText(g,text,f,new Size(400,40),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
  int w=s.Width+S(18);chip=new Rectangle(alignRight?area.Right-w:area.X,area.Y,w,area.Height);
  using(var p=Round(chip,chip.Height/2))using(var b=new SolidBrush(Color.FromArgb(46,color)))g.FillPath(b,p);
  TextRenderer.DrawText(g,text,f,chip,color,Center);
 }
}
public sealed class RoundButton:Button {
 public bool Primary,Nav,Danger;public Color Dot=Color.Empty,Under=Theme.Background;bool hover,down;
 public RoundButton(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;UseMnemonic=false;}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;down=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnEnabledChanged(EventArgs e){if(!Enabled){hover=false;down=false;}Invalidate();base.OnEnabledChanged(e);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Under);
  Color fill,border=Theme.Border,fore=ForeColor;bool selected=Nav&&BackColor!=Theme.Sidebar;
  if(Nav){fill=selected?BackColor:hover?Color.FromArgb(32,34,48):Theme.Sidebar;border=fill;if(!selected&&hover)fore=Theme.Text;}
  else if(!Enabled){fill=Color.FromArgb(32,34,47);border=Color.FromArgb(42,44,60);fore=Color.FromArgb(104,108,128);}
  else if(Primary){Color basis=Danger?Theme.Danger:Theme.Accent;fill=down?Theme.Mix(basis,Color.Black,0.18f):hover?Theme.Mix(basis,Color.White,0.14f):basis;border=fill;}
  else{fill=down?Color.FromArgb(52,54,76):hover?Color.FromArgb(41,43,61):Theme.Surface;if(hover)border=Color.FromArgb(72,75,98);if(Danger&&hover)fore=Theme.Danger;}
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(Nav?10:8))){using(var b=new SolidBrush(fill))g.FillPath(b,path);using(var pen=new Pen(border))g.DrawPath(pen,path);}
  if(Nav){
   if(Dot.A>0)using(var b=new SolidBrush(Dot))g.FillEllipse(b,Theme.S(16),Height/2-Theme.S(4),Theme.S(8),Theme.S(8));
   int split=Text.LastIndexOf("   ");string name=split>0?Text.Substring(0,split):Text,count=split>0?Text.Substring(split+3):"";
   TextRenderer.DrawText(g,name,Font,new Rectangle(Theme.S(34),0,Width-Theme.S(86),Height),fore,Theme.Line);
   if(count.Length>0){
    var pill=new Rectangle(Width-Theme.S(50),Height/2-Theme.S(11),Theme.S(36),Theme.S(22));
    using(var p=Theme.Round(pill,pill.Height/2))using(var b=new SolidBrush(selected?Color.FromArgb(90,Theme.Accent):Color.FromArgb(34,36,50)))g.FillPath(b,p);
    TextRenderer.DrawText(g,count,Font,pill,fore,Theme.Center);
   }
  }else TextRenderer.DrawText(g,Text,Font,new Rectangle(Theme.S(4),0,Width-Theme.S(8),Height),fore,Theme.Center);
  if(Focused&&ShowFocusCues)using(var p=Theme.Round(new Rectangle(2,2,Width-5,Height-5),Theme.S(7)))using(var pen=new Pen(Color.FromArgb(150,Primary?Color.White:Theme.Accent),1.5f))g.DrawPath(pen,p);
 }
}
public sealed class InputBox:Panel {
 public bool Active,Icon;
 public InputBox(){DoubleBuffered=true;BackColor=Theme.Input;ResizeRedraw=true;Cursor=Cursors.IBeam;}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent!=null?Parent.BackColor:Theme.Background);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(9)))using(var b=new SolidBrush(Theme.Input))using(var pen=new Pen(Active?Theme.Accent:Theme.Border,Active?1.6f:1f)){g.FillPath(b,path);g.DrawPath(pen,path);}
  if(Icon)using(var pen=new Pen(Active?Theme.Accent:Theme.Muted,1.8f*Theme.Scale){StartCap=LineCap.Round,EndCap=LineCap.Round}){
   int cx=Theme.S(15),cy=Height/2-Theme.S(8),d=Theme.S(12);g.DrawEllipse(pen,cx,cy,d,d);
   g.DrawLine(pen,cx+d-Theme.S(2),cy+d-Theme.S(2),cx+d+Theme.S(4),cy+d+Theme.S(4));
  }
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);foreach(Control c in Controls)if(c is TextBox){c.Focus();break;}}
}
public sealed class CoverBox:PictureBox {
 public string Initial="";public Color Under=Theme.Surface;public int Radius=8;
 public CoverBox(){DoubleBuffered=true;}
 protected override void OnPaintBackground(PaintEventArgs e){}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.Clear(Under);
  Theme.DrawCover(g,new Rectangle(0,0,Width-1,Height-1),Image,Initial,Theme.S(Radius));
 }
 protected override void Dispose(bool disposing){if(disposing&&Image!=null){Image old=Image;Image=null;old.Dispose();}base.Dispose(disposing);}
}
public sealed class Brand:Control {
 public Brand(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);BackColor=Color.Transparent;}
 protected override void OnPaint(PaintEventArgs e){
  Font f=Theme.Font(26,FontStyle.Bold);
  var s=TextRenderer.MeasureText(e.Graphics,"Ani",f,new Size(400,80),TextFormatFlags.NoPadding);
  TextRenderer.DrawText(e.Graphics,"Ani",f,new Point(0,0),Theme.Accent,TextFormatFlags.NoPadding);
  TextRenderer.DrawText(e.Graphics,"Lista",f,new Point(s.Width,0),Theme.Text,TextFormatFlags.NoPadding);
 }
}
public sealed class FlowPanel:FlowLayoutPanel { public FlowPanel(){DoubleBuffered=true;Theme.DarkScroll(this);} }
public sealed class LinePanel:Panel {
 public string Badge,YearText="";public Color BadgeColor=Theme.Accent;public double Progress=-1;
 public LinePanel(){DoubleBuffered=true;BackColor=Theme.Surface;ResizeRedraw=true;}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent!=null?Parent.BackColor:Theme.Background);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(12))){using(var b=new SolidBrush(BackColor))g.FillPath(b,path);using(var pen=new Pen(Theme.Border))g.DrawPath(pen,path);}
  int left=Theme.S(118);
  if(Badge!=null){
   Rectangle chip;Theme.Chip(g,Badge.ToUpper(),BadgeColor,new Rectangle(left,Theme.S(18),0,Theme.S(22)),false,out chip);
   if(YearText.Length>0)TextRenderer.DrawText(g,YearText,Theme.Font(9),new Rectangle(chip.Right+Theme.S(10),chip.Y,Theme.S(60),chip.Height),Theme.Muted,Theme.Line);
  }
  if(Progress>=0){
   int y=Theme.S(126),h=Theme.S(6),w=Math.Max(Theme.S(20),Width-left-Theme.S(66));
   using(var p=Theme.Round(new Rectangle(left,y,w,h),h/2))using(var b=new SolidBrush(Theme.Border))g.FillPath(b,p);
   int fill=(int)(w*Progress);if(fill>=h)using(var p=Theme.Round(new Rectangle(left,y,fill,h),h/2))using(var b=new SolidBrush(BadgeColor))g.FillPath(b,p);
   TextRenderer.DrawText(g,(int)Math.Round(Progress*100)+"%",Theme.Font(9,FontStyle.Bold),new Rectangle(left+w+Theme.S(10),y-Theme.S(7),Theme.S(46),Theme.S(20)),Theme.Muted,Theme.Line);
  }
 }
}
// Seletor de lista com três opções, no lugar do ComboBox claro do Windows.
public sealed class Segmented:Control {
 readonly string[] options;readonly Color[] colors;int selected,hover=-1;
 public event EventHandler SelectedIndexChanged;
 public Segmented(string[] items,Color[] tints){
  options=items;colors=tints;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
  TabStop=true;Cursor=Cursors.Hand;Font=Theme.Font(10,FontStyle.Bold);
 }
 public int SelectedIndex{get{return selected;}set{int v=Math.Max(0,Math.Min(options.Length-1,value));if(v==selected)return;selected=v;Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
 Rectangle Segment(int i){int pad=Theme.S(4),w=(Width-2*pad)/options.Length,x=pad+i*w,right=i==options.Length-1?Width-pad:x+w;return new Rectangle(x,pad,right-x-1,Height-2*pad-1);}
 int At(Point p){for(int i=0;i<options.Length;i++)if(Segment(i).Contains(p))return i;return -1;}
 protected override void OnMouseMove(MouseEventArgs e){int i=At(e.Location);if(i!=hover){hover=i;Invalidate();}base.OnMouseMove(e);}
 protected override void OnMouseLeave(EventArgs e){hover=-1;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){Focus();if(e.Button==MouseButtons.Left){int i=At(e.Location);if(i>=0)SelectedIndex=i;}base.OnMouseDown(e);}
 protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||base.IsInputKey(key);}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Left)SelectedIndex=selected-1;else if(e.KeyCode==Keys.Right)SelectedIndex=selected+1;base.OnKeyDown(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
 protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent!=null?Parent.BackColor:Theme.Background);
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(9)))using(var b=new SolidBrush(Theme.Input))using(var pen=new Pen(Focused?Theme.Accent:Theme.Border,Focused?1.6f:1f)){g.FillPath(b,path);g.DrawPath(pen,path);}
  for(int i=0;i<options.Length;i++){
   Rectangle r=Segment(i);bool on=i==selected;Color tint=colors[i];
   if(on||i==hover)using(var p=Theme.Round(r,Theme.S(7))){
    using(var b=new SolidBrush(on?Color.FromArgb(46,tint):Color.FromArgb(42,44,61)))g.FillPath(b,p);
    if(on)using(var pen=new Pen(Color.FromArgb(170,tint)))g.DrawPath(pen,p);
   }
   int dot=Theme.S(8),gap=Theme.S(8);Size s=TextRenderer.MeasureText(g,options[i],Font,new Size(400,40),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
   int x=r.X+Math.Max(Theme.S(6),(r.Width-dot-gap-s.Width)/2);
   using(var b=new SolidBrush(on?tint:Color.FromArgb(130,tint)))g.FillEllipse(b,x,r.Y+r.Height/2-dot/2,dot,dot);
   TextRenderer.DrawText(g,options[i],Font,new Rectangle(x+dot+gap,r.Y,r.Right-x-dot-gap-Theme.S(4),r.Height),on||i==hover?Theme.Text:Theme.Muted,Theme.Line);
  }
 }
}
// Campo numérico escuro com botões − e +, no lugar do NumericUpDown claro do Windows.
public sealed class Stepper:Panel {
 readonly TextBox box;readonly RoundButton minus,plus;int value;bool locked,active,syncing;
 public int Maximum=100000;public event EventHandler ValueChanged;
 public Stepper(){
  DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Input;
  box=Theme.TextBox();box.TextAlign=HorizontalAlignment.Center;box.Font=Theme.Font(12,FontStyle.Bold);box.MaxLength=6;box.Text="0";
  minus=Small("−");plus=Small("+");
  minus.Click+=delegate{Value=value-1;};plus.Click+=delegate{Value=value+1;};
  box.KeyPress+=delegate(object s,KeyPressEventArgs e){if(!char.IsControl(e.KeyChar)&&(e.KeyChar<'0'||e.KeyChar>'9'))e.Handled=true;};
  box.TextChanged+=delegate{if(syncing)return;int v;if(int.TryParse(box.Text,NumberStyles.None,CultureInfo.InvariantCulture,out v))SetValue(v,v>Maximum);};
  box.KeyDown+=delegate(object s,KeyEventArgs e){
   if(locked)return;
   if(e.KeyCode==Keys.Up){Value=value+1;e.Handled=true;e.SuppressKeyPress=true;}
   else if(e.KeyCode==Keys.Down){Value=value-1;e.Handled=true;e.SuppressKeyPress=true;}
  };
  box.Enter+=delegate{active=true;Invalidate();};box.Leave+=delegate{active=false;SetValue(value,true);Invalidate();};
  Controls.Add(box);Controls.Add(minus);Controls.Add(plus);
 }
 RoundButton Small(string text){return new RoundButton{Text=text,Font=Theme.Font(12,FontStyle.Bold),Under=Theme.Input,BackColor=Theme.Surface,ForeColor=Theme.Text,Cursor=Cursors.Hand,TabStop=false};}
 public int Value{get{return value;}set{SetValue(value,true);}}
 public bool Locked{get{return locked;}set{
  locked=value;box.ReadOnly=value;box.TabStop=!value;box.ForeColor=value?Theme.Muted:Theme.Text;box.BackColor=value?Theme.Locked:Theme.Input;
  minus.Enabled=plus.Enabled=!value;minus.Under=plus.Under=value?Theme.Locked:Theme.Input;minus.Invalidate();plus.Invalidate();Invalidate();
 }}
 void SetValue(int v,bool updateText){
  v=Math.Max(0,Math.Min(Maximum,v));bool changed=v!=value;value=v;
  string text=v.ToString(CultureInfo.InvariantCulture);if(updateText&&box.Text!=text){syncing=true;box.Text=text;syncing=false;}
  if(changed&&ValueChanged!=null)ValueChanged(this,EventArgs.Empty);
 }
 protected override void OnLayout(LayoutEventArgs e){
  base.OnLayout(e);if(box==null)return;
  int pad=Theme.S(5),size=Math.Max(10,Height-2*pad);minus.SetBounds(pad,pad,size,size);plus.SetBounds(Width-pad-size,pad,size,size);
  int left=pad+size+Theme.S(6);box.SetBounds(left,(Height-box.PreferredHeight)/2,Math.Max(10,Width-2*left),box.PreferredHeight);
 }
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent!=null?Parent.BackColor:Theme.Background);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(9)))using(var b=new SolidBrush(locked?Theme.Locked:Theme.Input))using(var pen=new Pen(active&&!locked?Theme.Accent:Theme.Border,active&&!locked?1.6f:1f)){g.FillPath(b,path);g.DrawPath(pen,path);}
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(!locked)box.Focus();}
}
// Janela de aviso/confirmação no tema escuro, no lugar do MessageBox branco.
public sealed class Notice:Form {
 Notice(string title,string message,string ok,string cancel,bool danger){
  Text="AniLista";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font(10);Theme.DarkTitle(this);
  FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowIcon=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  int width=Theme.S(470),pad=Theme.S(28),inner=width-2*pad;
  var bar=new Panel{BackColor=danger?Theme.Danger:Theme.Accent};bar.SetBounds(pad,Theme.S(27),Theme.S(4),Theme.S(26));Controls.Add(bar);
  var head=Theme.Label(title,15,Theme.Text,FontStyle.Bold);head.AutoEllipsis=true;head.SetBounds(pad+Theme.S(14),Theme.S(20),inner-Theme.S(14),Theme.S(38));Controls.Add(head);
  Font font=Theme.Font(10.5f);int height=TextRenderer.MeasureText(message,font,new Size(inner,10000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
  var body=Theme.Label(message,10.5f,Theme.Soft);body.SetBounds(pad,Theme.S(70),inner,height+Theme.S(8));Controls.Add(body);
  int top=body.Bottom+Theme.S(24);
  var confirm=Theme.Button(ok,true);confirm.Danger=danger;confirm.DialogResult=DialogResult.OK;
  int cw=Math.Max(Theme.S(112),TextRenderer.MeasureText(ok,confirm.Font).Width+Theme.S(40));confirm.SetBounds(width-pad-cw,top,cw,Theme.S(40));Controls.Add(confirm);AcceptButton=confirm;
  if(cancel!=null){
   var back=Theme.Button(cancel);back.DialogResult=DialogResult.Cancel;int bw=Math.Max(Theme.S(112),TextRenderer.MeasureText(cancel,back.Font).Width+Theme.S(40));
   back.SetBounds(confirm.Left-Theme.S(10)-bw,top,bw,Theme.S(40));Controls.Add(back);CancelButton=back;if(danger)ActiveControl=back;
  }else CancelButton=confirm;
  ClientSize=new Size(width,top+Theme.S(40)+Theme.S(24));
 }
 public static bool Ask(IWin32Window owner,string title,string message,string ok,string cancel,bool danger=false){
  using(var n=new Notice(title,message,ok,cancel,danger)){
   if(owner==null){n.StartPosition=FormStartPosition.CenterScreen;n.ShowInTaskbar=true;n.ShowIcon=Theme.AppIcon!=null;if(Theme.AppIcon!=null)n.Icon=Theme.AppIcon;}
   return n.ShowDialog(owner)==DialogResult.OK;
  }
 }
 public static void Tell(IWin32Window owner,string title,string message){Ask(owner,title,message,"OK",null);}
}
public sealed class SearchResult {
 public Anime Anime;public string Alternative="",Format="",Thumb="";public Image Image;
 public override string ToString(){return Anime.Title;}
}
public sealed class CatalogException:Exception { public CatalogException(string message):base(message){} }
public static class Catalog {
 public static string CoverFolder=Path.Combine(Path.GetTempPath(),"AniLista-capas");
 static readonly HttpClient Client=CreateClient();
 static readonly Dictionary<int,byte[]> Memory=new Dictionary<int,byte[]>();
 static HttpClient CreateClient(){
  try{ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;}catch{}
  var c=new HttpClient{Timeout=TimeSpan.FromSeconds(18)};c.DefaultRequestHeaders.UserAgent.ParseAdd("AniLista/1.1");return c;
 }
 static Dictionary<string,object> Obj(object o){return o as Dictionary<string,object>;}
 static object Get(Dictionary<string,object> d,string key){object v;return d!=null&&d.TryGetValue(key,out v)?v:null;}
 static int Int(object o){try{return o==null?0:Convert.ToInt32(o,CultureInfo.InvariantCulture);}catch{return 0;}}
 static string Str(object o){return o==null?"":Convert.ToString(o,CultureInfo.InvariantCulture);}
 static string FormatName(string f){
  switch(f){case "TV":return "Série de TV";case "TV_SHORT":return "TV curta";case "MOVIE":return "Filme";case "SPECIAL":return "Especial";case "OVA":return "OVA";case "ONA":return "ONA";case "MUSIC":return "Clipe musical";default:return "";}
 }
 public static async Task<List<SearchResult>> Search(string text,CancellationToken token){
  var json=new JavaScriptSerializer{MaxJsonLength=4000000};
  var query=new {query="query($search:String){Page(perPage:20){media(search:$search,type:ANIME,sort:POPULARITY_DESC,isAdult:false){id title{romaji english} format episodes seasonYear startDate{year} coverImage{large medium}}}}",variables=new {search=text}};
  using(var request=new HttpRequestMessage(HttpMethod.Post,"https://graphql.anilist.co")){
   request.Content=new StringContent(json.Serialize(query),Encoding.UTF8,"application/json");request.Headers.Accept.ParseAdd("application/json");
   HttpResponseMessage response;
   try{response=await Client.SendAsync(request,token);}
   catch(HttpRequestException){throw new CatalogException("Não foi possível acessar o catálogo. Confira a conexão ou use o cadastro manual.");}
   using(response){
    if((int)response.StatusCode==429)throw new CatalogException("Muitas buscas em pouco tempo. Aguarde alguns segundos e tente de novo.");
    if(!response.IsSuccessStatusCode)throw new CatalogException("O catálogo está indisponível agora (erro "+(int)response.StatusCode+"). Tente novamente ou use o cadastro manual.");
    var root=Obj(json.DeserializeObject(await response.Content.ReadAsStringAsync()));
    var rows=Get(Obj(Get(Obj(Get(root,"data")),"Page")),"media") as object[];
    if(rows==null)throw new CatalogException("O catálogo não retornou os resultados. Tente novamente.");
    var result=new List<SearchResult>();
    foreach(object item in rows){
     var row=Obj(item);if(row==null)continue;
     var titles=Obj(Get(row,"title"));var cover=Obj(Get(row,"coverImage"));
     string romaji=Str(Get(titles,"romaji")).Trim(),english=Str(Get(titles,"english")).Trim(),title=romaji.Length>0?romaji:english;
     if(title.Length==0)continue;if(title.Length>180)title=title.Substring(0,180);
     int year=Int(Get(row,"seasonYear"));if(year<=0)year=Int(Get(Obj(Get(row,"startDate")),"year"));
     result.Add(new SearchResult{
      Anime=new Anime{CatalogId=Int(Get(row,"id")),Title=title,Total=Math.Max(0,Math.Min(100000,Int(Get(row,"episodes")))),Year=Math.Max(0,year),Cover=Str(Get(cover,"large"))},
      Alternative=String.Equals(english,title,StringComparison.OrdinalIgnoreCase)?"":english,Format=FormatName(Str(Get(row,"format"))),Thumb=Str(Get(cover,"medium"))
     });
    }
    return result;
   }
  }
 }
 static bool TryUri(string url,out Uri uri){return Uri.TryCreate(url??"",UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host.EndsWith(".anilist.co",StringComparison.OrdinalIgnoreCase);}
 static async Task<byte[]> Download(Uri uri,CancellationToken token){
  using(var response=await Client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token)){
   if(!response.IsSuccessStatusCode)return null;
   long? length=response.Content.Headers.ContentLength;if(length.HasValue&&length.Value>4000000)return null;
   byte[] bytes=await response.Content.ReadAsByteArrayAsync();return bytes.Length>4000000?null:bytes;
  }
 }
 static Image Decode(byte[] bytes){try{using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream))return new Bitmap(image);}catch{return null;}}
 public static async Task<Image> LoadImage(string url,CancellationToken token){
  Uri uri;if(!TryUri(url,out uri))return null;
  try{byte[] bytes=await Download(uri,token);return bytes==null?null:Decode(bytes);}catch{return null;}
 }
 public static async Task LoadCover(PictureBox picture,Anime anime){
  if(anime==null||anime.CatalogId<=0)return;Uri uri;if(!TryUri(anime.Cover,out uri))return;
  int id=anime.CatalogId;string folder=CoverFolder,file=Path.Combine(folder,id+".jpg");
  try{
   byte[] bytes;
   if(!Memory.TryGetValue(id,out bytes)){
    bytes=await Task.Run<byte[]>(()=>{try{return File.Exists(file)?File.ReadAllBytes(file):null;}catch{return null;}});
    if(bytes==null){
     bytes=await Download(uri,CancellationToken.None);if(bytes==null)return;
     byte[] copy=bytes;await Task.Run(()=>{try{Directory.CreateDirectory(folder);File.WriteAllBytes(file,copy);}catch{}});
    }
    if(Memory.Count>400)Memory.Clear();Memory[id]=bytes;
   }
   if(picture.IsDisposed)return;
   Image image=Decode(bytes);
   if(image==null){Memory.Remove(id);try{File.Delete(file);}catch{}return;}
   if(picture.IsDisposed){image.Dispose();return;}
   Image old=picture.Image;picture.Image=image;if(old!=null)old.Dispose();
  }catch{}
 }
}
public sealed class EditorForm:Form {
 readonly Anime entry;readonly Func<Anime,bool> save;
 TextBox title;Segmented status;Stepper episode,total;Label error;CoverBox cover;bool updating;
 public EditorForm(Anime anime,bool isNew,string initialStatus,Func<Anime,bool> onSave){
  entry=anime.Copy();save=onSave;if(isNew)entry.Status=initialStatus=="watching"||initialStatus=="completed"?initialStatus:"planned";
  Text=isNew?"Adicionar anime · AniLista":"Editar anime · AniLista";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font(10);Theme.DarkTitle(this);
  FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowIcon=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;ClientSize=Theme.S(640,472);
  var heading=Theme.Label(isNew?"Adicionar à sua biblioteca":"Editar anime",19,Theme.Text,FontStyle.Bold);Theme.Place(heading,26,18,588,40);Controls.Add(heading);
  var sub=Theme.Label(!isNew?"Atualize a lista ou o seu progresso.":entry.CatalogId>0?"Confira os dados do catálogo e escolha a lista.":"Preencha os dados do anime que você quer guardar.",10,Theme.Muted);Theme.Place(sub,28,58,584,24);Controls.Add(sub);
  cover=new CoverBox{Initial=Theme.Initial(entry.Title),Under=Theme.Background};Theme.Place(cover,28,100,120,170);Controls.Add(cover);
  if(entry.CatalogId>0){var ignored=Catalog.LoadCover(cover,entry);}
  if(entry.Year>0){var year=Theme.Label("Lançamento: "+entry.Year,9,Theme.Muted);year.TextAlign=ContentAlignment.MiddleCenter;Theme.Place(year,28,276,120,22);Controls.Add(year);}
  AddLabel("Nome do anime",172,96,440);title=Theme.TextBox();title.Text=entry.Title;title.MaxLength=180;Controls.Add(Theme.Wrap(title,172,122,440,42));
  title.TextChanged+=delegate{cover.Initial=Theme.Initial(title.Text);cover.Invalidate();};
  AddLabel("Lista",172,176,440);
  status=new Segmented(new[]{"Quero assistir","Assistindo","Concluídos"},new[]{Theme.Warn,Theme.Accent,Theme.Success});Theme.Place(status,172,202,440,42);
  status.SelectedIndex=entry.Status=="watching"?1:entry.Status=="completed"?2:0;Controls.Add(status);
  AddLabel("Episódio atual",172,258,210);AddLabel("Total de episódios",394,258,218);
  episode=new Stepper();Theme.Place(episode,172,284,210,42);episode.Value=entry.Episode;Controls.Add(episode);
  total=new Stepper();Theme.Place(total,394,284,218,42);total.Value=entry.Total;Controls.Add(total);
  var hint=Theme.Label("0 = ainda não informado",9,Theme.Muted);Theme.Place(hint,396,330,216,20);Controls.Add(hint);
  error=Theme.Label("",10,Theme.Danger);Theme.Place(error,28,356,584,42);Controls.Add(error);
  var cancel=Theme.Button("Cancelar");Theme.Place(cancel,316,408,112,42);cancel.DialogResult=DialogResult.Cancel;
  var confirm=Theme.Button(isNew?"Adicionar anime":"Salvar alterações",true);Theme.Place(confirm,440,408,172,42);
  confirm.Click+=delegate{
   try{
    Anime updated=entry.Copy();updated.Title=title.Text;updated.Status=status.SelectedIndex==1?"watching":status.SelectedIndex==2?"completed":"planned";
    updated.Episode=episode.Value;updated.Total=total.Value;updated.Validate();error.Text="";
    if(save(updated)){DialogResult=DialogResult.OK;Close();}
   }catch(Exception ex){error.Text=ex.Message;}
  };
  Controls.Add(cancel);Controls.Add(confirm);AcceptButton=confirm;CancelButton=cancel;
  status.SelectedIndexChanged+=delegate{UpdateNumbers();};total.ValueChanged+=delegate{UpdateNumbers();};UpdateNumbers();
  ActiveControl=title;Shown+=delegate{title.Focus();title.Select(title.TextLength,0);};
 }
 void AddLabel(string text,int x,int y,int w){var label=Theme.Label(text,10,Theme.Muted);Theme.Place(label,x,y,w,24);Controls.Add(label);}
 void UpdateNumbers(){
  if(updating)return;updating=true;int s=status.SelectedIndex;
  episode.Locked=!(s==1||(s==2&&total.Value==0));
  if(s==0)episode.Value=0;else if(s==2&&total.Value>0)episode.Value=total.Value;
  updating=false;
 }
}
public sealed class SearchForm:Form {
 readonly Func<SearchForm,Anime,bool> choose;readonly ICollection<int> owned;
 TextBox search;RoundButton searchButton,selectButton;ListBox results;Label message,placeholder;
 System.Windows.Forms.Timer typing;string lastQuery="";
 readonly List<SearchResult> items=new List<SearchResult>();CancellationTokenSource cancellation;int hover=-1;
 public SearchForm(Func<SearchForm,Anime,bool> onChoose,ICollection<int> library=null){
  choose=onChoose;owned=library??new List<int>();
  Text="Adicionar anime · AniLista";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font(10);Theme.DarkTitle(this);AutoScaleMode=AutoScaleMode.None;
  StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;ClientSize=Theme.S(780,640);MinimumSize=SizeFromClientSize(Theme.S(640,520));
  if(Theme.AppIcon!=null)Icon=Theme.AppIcon;
  var heading=Theme.Label("Encontre seu próximo anime",21,Theme.Text,FontStyle.Bold);Theme.Place(heading,26,18,724,46);heading.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(heading);
  var hint=Theme.Label("Busque pelo nome em português, inglês ou japonês — ou cadastre manualmente.",10,Theme.Muted);Theme.Place(hint,28,64,724,24);hint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(hint);
  search=Theme.TextBox();search.MaxLength=100;Theme.Cue(search,"Digite o nome do anime");Controls.Add(Theme.Wrap(search,28,100,588,44,AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,true));
  searchButton=Theme.Button("Buscar",true);Theme.Place(searchButton,630,100,122,44);searchButton.Anchor=AnchorStyles.Top|AnchorStyles.Right;searchButton.Click+=async delegate{typing.Stop();await RunSearch(false);};Controls.Add(searchButton);AcceptButton=searchButton;
  // Busca ao vivo: depois de uma pequena pausa na digitação, os resultados aparecem sozinhos.
  typing=new System.Windows.Forms.Timer{Interval=380};
  typing.Tick+=async delegate{typing.Stop();await RunSearch(true);};
  search.TextChanged+=delegate{typing.Stop();if(search.Text.Trim().Length<2)ResetResults();else typing.Start();};
  search.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Down&&results.Items.Count>0){e.Handled=e.SuppressKeyPress=true;results.Focus();if(results.SelectedIndex<0)results.SelectedIndex=0;}};
  message=Theme.Label("Digite o nome de um anime para começar.",10,Theme.Muted);message.AutoEllipsis=true;Theme.Place(message,28,154,724,26);message.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(message);
  var frame=new LinePanel{Padding=new Padding(Theme.S(6))};Theme.Place(frame,28,188,724,368);frame.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(frame);
  results=new ListBox{DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=Math.Min(255,Theme.S(84)),BorderStyle=BorderStyle.None,BackColor=Theme.Surface,ForeColor=Theme.Text,IntegralHeight=false,Dock=DockStyle.Fill,Font=Theme.Font(11)};
  Theme.DarkScroll(results);frame.Controls.Add(results);
  placeholder=Theme.Label("As capas e os detalhes dos animes aparecem aqui.",10.5f,Theme.Muted);placeholder.BackColor=Theme.Surface;placeholder.TextAlign=ContentAlignment.MiddleCenter;placeholder.Dock=DockStyle.Fill;frame.Controls.Add(placeholder);placeholder.BringToFront();
  results.DrawItem+=DrawResult;
  results.MouseMove+=delegate(object s,MouseEventArgs e){int i=results.IndexFromPoint(e.Location);if(i!=hover){int old=hover;hover=i;Repaint(old);Repaint(hover);}};
  results.MouseLeave+=delegate{int old=hover;hover=-1;Repaint(old);};
  results.SelectedIndexChanged+=delegate{selectButton.Enabled=results.SelectedIndex>=0;};
  results.DoubleClick+=delegate{if(results.IndexFromPoint(results.PointToClient(Cursor.Position))>=0)SelectResult();};
  results.Enter+=delegate{AcceptButton=selectButton;};search.Enter+=delegate{AcceptButton=searchButton;};
  results.Resize+=delegate{results.Invalidate();};
  var credit=Theme.Label("Catálogo: AniList",9,Theme.Muted);Theme.Place(credit,28,586,240,24);credit.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;Controls.Add(credit);
  var manual=Theme.Button("Cadastro manual");Theme.Place(manual,404,576,170,44);manual.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;manual.Click+=delegate{Choose(new Anime());};Controls.Add(manual);
  selectButton=Theme.Button("Selecionar anime",true);Theme.Place(selectButton,586,576,166,44);selectButton.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;selectButton.Enabled=false;selectButton.Click+=delegate{SelectResult();};Controls.Add(selectButton);
  FormClosed+=delegate{typing.Stop();typing.Dispose();if(cancellation!=null){cancellation.Cancel();cancellation=null;}ClearItems();};Shown+=delegate{search.Focus();};
 }
 void Repaint(int index){if(index>=0&&index<results.Items.Count)results.Invalidate(results.GetItemRectangle(index));}
 void Choose(Anime anime){if(choose(this,anime)){DialogResult=DialogResult.OK;Close();}}
 void SelectResult(){int i=results.SelectedIndex;if(i>=0&&i<items.Count)Choose(items[i].Anime.Copy());}
 void ClearItems(){
  if(!results.IsDisposed)results.Items.Clear();
  foreach(SearchResult r in items)if(r.Image!=null){r.Image.Dispose();r.Image=null;}
  items.Clear();hover=-1;
 }
 void SetMessage(string text,bool warning){message.ForeColor=warning?Theme.Warn:Theme.Muted;message.Text=text;}
 void ResetResults(){
  if(cancellation!=null){cancellation.Cancel();cancellation=null;}
  lastQuery="";results.BeginUpdate();ClearItems();results.EndUpdate();
  placeholder.Text="As capas e os detalhes dos animes aparecem aqui.";placeholder.Visible=true;
  searchButton.Enabled=true;selectButton.Enabled=false;
  SetMessage(search.Text.Trim().Length==0?"Digite o nome de um anime para começar.":"Continue digitando — os resultados aparecem sozinhos.",false);
 }
 async Task RunSearch(bool live){
  string text=search.Text.Trim();
  if(text.Length<2){if(live)ResetResults();else SetMessage("Digite pelo menos 2 caracteres.",true);return;}
  if(live&&text==lastQuery)return;lastQuery=text;
  if(cancellation!=null)cancellation.Cancel();var request=new CancellationTokenSource();cancellation=request;
  if(!live){searchButton.Enabled=false;selectButton.Enabled=false;}SetMessage("Buscando \""+text+"\" no catálogo...",false);
  try{
   List<SearchResult> found=await Catalog.Search(text,request.Token);
   if(IsDisposed||request!=cancellation)return;
   results.BeginUpdate();ClearItems();items.AddRange(found);foreach(SearchResult r in found)results.Items.Add(r);results.EndUpdate();
   placeholder.Visible=found.Count==0;placeholder.Text=found.Count==0?"Nenhum resultado para \""+text+"\".":"";
   SetMessage(found.Count==0?"Nenhum anime encontrado. Tente outro nome ou use o cadastro manual.":found.Count==1?"1 resultado. Clique duas vezes ou use Selecionar anime.":found.Count+" resultados. Clique duas vezes ou use Selecionar anime.",false);
   foreach(SearchResult r in found){var ignored=LoadThumb(r,request.Token);}
  }
  catch(OperationCanceledException){if(!IsDisposed&&request==cancellation){lastQuery="";SetMessage("A busca demorou demais. Tente novamente ou use o cadastro manual.",true);}}
  catch(CatalogException ex){if(!IsDisposed&&request==cancellation){lastQuery="";SetMessage(ex.Message,true);}}
  catch(Exception){if(!IsDisposed&&request==cancellation){lastQuery="";SetMessage("Não foi possível ler a resposta do catálogo. Tente novamente ou use o cadastro manual.",true);}}
  finally{if(!IsDisposed&&request==cancellation){searchButton.Enabled=true;selectButton.Enabled=results.SelectedIndex>=0;}}
 }
 async Task LoadThumb(SearchResult result,CancellationToken token){
  Image image=await Catalog.LoadImage(result.Thumb.Length>0?result.Thumb:result.Anime.Cover,token);
  if(image==null)return;
  int index=items.IndexOf(result);
  if(IsDisposed||token.IsCancellationRequested||index<0){image.Dispose();return;}
  result.Image=image;Repaint(index);
 }
 void DrawResult(object sender,DrawItemEventArgs e){
  var g=e.Graphics;using(var bg=new SolidBrush(Theme.Surface))g.FillRectangle(bg,e.Bounds);
  if(e.Index<0||e.Index>=items.Count)return;
  g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
  SearchResult r=items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;
  var card=new Rectangle(e.Bounds.X+Theme.S(2),e.Bounds.Y+Theme.S(3),e.Bounds.Width-Theme.S(4)-1,e.Bounds.Height-Theme.S(6)-1);
  if(selected||e.Index==hover)using(var p=Theme.Round(card,Theme.S(10))){
   using(var b=new SolidBrush(selected?Theme.Selected:Theme.Row))g.FillPath(b,p);
   if(selected)using(var pen=new Pen(Color.FromArgb(150,Theme.Accent)))g.DrawPath(pen,p);
  }
  // Ícone (capa) do anime à esquerda de cada resultado.
  int th=card.Height-Theme.S(14),tw=(int)(th*0.72f);var thumb=new Rectangle(card.X+Theme.S(9),card.Y+Theme.S(7),tw,th);
  Theme.DrawCover(g,thumb,r.Image,Theme.Initial(r.Anime.Title),Theme.S(6));
  int x=thumb.Right+Theme.S(14),right=card.Right-Theme.S(14);
  if(r.Anime.CatalogId>0&&owned.Contains(r.Anime.CatalogId)){Rectangle chip;Theme.Chip(g,"NA BIBLIOTECA",Theme.Success,new Rectangle(x,card.Y+Theme.S(12),right-x,Theme.S(22)),true,out chip);right=chip.X-Theme.S(10);}
  bool alt=r.Alternative.Length>0;int top=card.Y+(alt?Theme.S(9):Theme.S(15));
  TextRenderer.DrawText(g,r.Anime.Title,Theme.Font(11.5f,FontStyle.Bold),new Rectangle(x,top,Math.Max(10,right-x),Theme.S(24)),Theme.Text,Theme.Line);
  if(alt)TextRenderer.DrawText(g,r.Alternative,Theme.Font(9.5f),new Rectangle(x,top+Theme.S(24),Math.Max(10,card.Right-Theme.S(14)-x),Theme.S(20)),Theme.Soft,Theme.Line);
  string episodes=r.Anime.Total==1?"1 episódio":r.Anime.Total>1?r.Anime.Total+" episódios":"Episódios não informados";
  string meta=String.Join("  ·  ",new[]{r.Format,r.Anime.Year>0?r.Anime.Year.ToString():"",episodes}.Where(s=>s.Length>0).ToArray());
  TextRenderer.DrawText(g,meta,Theme.Font(9),new Rectangle(x,top+(alt?Theme.S(45):Theme.S(28)),Math.Max(10,card.Right-Theme.S(14)-x),Theme.S(20)),Theme.Muted,Theme.Line);
 }
}
public sealed class MainForm:Form {
 static readonly string[] StatusKeys={"watching","planned","completed"};
 readonly LibraryStore store;List<Anime> entries;string status="watching";
 readonly RoundButton[] navigation=new RoundButton[3];Label heading,summary,feedback,stats;TextBox filter;FlowLayoutPanel cards;Panel main;System.Windows.Forms.Timer filterTimer;
 public MainForm(LibraryStore library,List<Anime> data){
  store=library;entries=data??new List<Anime>();Catalog.CoverFolder=Path.Combine(store.Folder,"capas");
  Text="AniLista — Minha biblioteca de animes";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font(10);Theme.DarkTitle(this);
  StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.None;MinimumSize=Theme.S(920,600);
  Rectangle work=Screen.PrimaryScreen.WorkingArea;Size=new Size(Math.Min(Theme.S(1220),work.Width-40),Math.Min(Theme.S(800),work.Height-40));
  if(Theme.AppIcon!=null)Icon=Theme.AppIcon;
  BuildLayout();Render(false);
  if(store.Recovered)Shown+=delegate{Notice.Tell(this,"Biblioteca recuperada","A cópia de segurança da biblioteca foi recuperada. O arquivo anterior foi preservado.");};
  KeyPreview=true;KeyDown+=delegate(object sender,KeyEventArgs e){
   if(e.Control&&e.KeyCode==Keys.N){e.Handled=true;e.SuppressKeyPress=true;AddAnime();}
   else if(e.Control&&e.KeyCode==Keys.F){e.Handled=true;e.SuppressKeyPress=true;filter.Focus();filter.SelectAll();}
  };
  FormClosed+=delegate{filterTimer.Dispose();DisposeCards();};
 }
 void BuildLayout(){
  var sidebar=new Panel{Dock=DockStyle.Left,Width=Theme.S(236),BackColor=Theme.Sidebar};Controls.Add(sidebar);
  sidebar.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(Theme.Border))e.Graphics.DrawLine(p,sidebar.Width-1,0,sidebar.Width-1,sidebar.Height);};
  var logo=new Brand();Theme.Place(logo,24,30,196,52);sidebar.Controls.Add(logo);
  var section=Theme.Label("MINHA BIBLIOTECA",8.5f,Theme.Muted,FontStyle.Bold);Theme.Place(section,26,112,190,20);sidebar.Controls.Add(section);
  for(int i=0;i<StatusKeys.Length;i++){
   string selected=StatusKeys[i];var button=new RoundButton{Nav=true,Under=Theme.Sidebar,Dot=Theme.StatusColor(selected),Text=Theme.StatusName(selected),Font=Theme.Font(10,FontStyle.Bold),Cursor=Cursors.Hand,BackColor=Theme.Sidebar,ForeColor=Theme.Muted};
   Theme.Place(button,18,142+i*54,200,46);button.Click+=delegate{status=selected;filter.Clear();Render(false);};navigation[i]=button;sidebar.Controls.Add(button);
  }
  stats=Theme.Label("",9.5f,Theme.Muted);stats.Size=Theme.S(196,66);sidebar.Controls.Add(stats);
  EventHandler placeStats=delegate{stats.Location=new Point(Theme.S(26),Math.Max(Theme.S(320),sidebar.Height-stats.Height-Theme.S(18)));};
  sidebar.Resize+=placeStats;placeStats(sidebar,EventArgs.Empty);
  main=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Background,Padding=new Padding(Theme.S(30),Theme.S(22),Theme.S(24),Theme.S(8))};Controls.Add(main);main.BringToFront();
  var header=new Panel{Dock=DockStyle.Top,Height=Theme.S(96)};
  heading=Theme.Label("Assistindo",26,Theme.Text,FontStyle.Bold);Theme.Place(heading,0,0,520,50);header.Controls.Add(heading);
  summary=Theme.Label("",11,Theme.Muted);Theme.Place(summary,2,52,520,26);header.Controls.Add(summary);
  var add=Theme.Button("+  Adicionar anime",true);add.Size=Theme.S(186,44);add.Click+=delegate{AddAnime();};header.Controls.Add(add);
  header.Resize+=delegate{add.Location=new Point(header.Width-add.Width,Theme.S(6));heading.Width=summary.Width=Math.Max(Theme.S(180),header.Width-add.Width-Theme.S(16));};
  var searchRow=new Panel{Dock=DockStyle.Top,Height=Theme.S(60)};
  filter=Theme.TextBox();filter.MaxLength=100;Theme.Cue(filter,"Filtrar por nome  (Ctrl+F)");searchRow.Controls.Add(Theme.Wrap(filter,0,0,360,40,AnchorStyles.Top|AnchorStyles.Left,true));
  filter.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Escape&&filter.TextLength>0){filter.Clear();e.Handled=true;e.SuppressKeyPress=true;}};
  filterTimer=new System.Windows.Forms.Timer{Interval=170};filterTimer.Tick+=delegate{filterTimer.Stop();RenderCards(false);};filter.TextChanged+=delegate{filterTimer.Stop();filterTimer.Start();};
  var footer=new Panel{Dock=DockStyle.Bottom,Height=Theme.S(26)};feedback=Theme.Label("Salvo automaticamente neste computador",9,Theme.Muted);feedback.Dock=DockStyle.Fill;feedback.TextAlign=ContentAlignment.MiddleLeft;footer.Controls.Add(feedback);
  cards=new FlowPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true,BackColor=Theme.Background,Padding=new Padding(0),FlowDirection=FlowDirection.LeftToRight};cards.Resize+=delegate{ResizeCards();};
  main.Controls.Add(cards);main.Controls.Add(footer);main.Controls.Add(searchRow);main.Controls.Add(header);
 }
 void DisposeCards(){foreach(Control control in cards.Controls.Cast<Control>().ToArray())control.Dispose();cards.Controls.Clear();}
 void Render(bool keepScroll){
  heading.Text=Theme.StatusName(status);
  for(int i=0;i<StatusKeys.Length;i++){
   string key=StatusKeys[i];int count=entries.Count(a=>a.Status==key);navigation[i].Text=Theme.StatusName(key)+"   "+count;
   navigation[i].BackColor=key==status?Theme.Selected:Theme.Sidebar;navigation[i].ForeColor=key==status?Theme.SelectedText:Theme.Muted;navigation[i].Invalidate();
  }
  int selectedCount=entries.Count(a=>a.Status==status);summary.Text=selectedCount==1?"1 anime nesta lista":selectedCount+" animes nesta lista";
  int watched=entries.Where(a=>a.Status!="planned").Sum(a=>a.Episode);
  stats.Text=(entries.Count==1?"1 anime":entries.Count+" animes")+" na biblioteca\n"+(watched==1?"1 episódio assistido":watched+" episódios assistidos")+"\nSalvo neste computador";
  RenderCards(keepScroll);
 }
 void RenderCards(bool keepScroll){
  int scroll=keepScroll?-cards.AutoScrollPosition.Y:0;
  cards.SuspendLayout();DisposeCards();
  string text=filter.Text.Trim();
  var visible=entries.Where(a=>a.Status==status&&(text.Length==0||a.Title.IndexOf(text,StringComparison.CurrentCultureIgnoreCase)>=0)).OrderByDescending(a=>a.Updated,StringComparer.Ordinal).ThenBy(a=>a.Title).ToList();
  if(visible.Count==0)cards.Controls.Add(CreateEmpty(text.Length>0));
  else foreach(Anime anime in visible)cards.Controls.Add(CreateCard(anime));
  ResizeCards();cards.ResumeLayout(true);
  if(scroll>0)cards.AutoScrollPosition=new Point(0,scroll);
 }
 Panel CreateEmpty(bool filtered){
  var empty=new LinePanel{Width=Theme.S(760),Height=Theme.S(220),Margin=new Padding(0,Theme.S(4),0,Theme.S(14)),Tag="empty"};
  var title=Theme.Label(filtered?"Nenhum anime com esse nome":"Sua lista está pronta para começar",17,Theme.Text,FontStyle.Bold);title.BackColor=Theme.Surface;title.SetBounds(Theme.S(28),Theme.S(32),empty.Width-Theme.S(56),Theme.S(36));title.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;empty.Controls.Add(title);
  string hint=status=="watching"?"Adicione o que está assistindo e registre seu episódio atual.":status=="planned"?"Guarde aqui os animes que você quer assistir depois.":"Reúna os animes que você já terminou.";
  var detail=Theme.Label(filtered?"Tente outro nome ou limpe o filtro (Esc).":hint,11,Theme.Muted);detail.BackColor=Theme.Surface;detail.SetBounds(Theme.S(28),Theme.S(78),empty.Width-Theme.S(56),Theme.S(48));detail.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;empty.Controls.Add(detail);
  var button=Theme.Button(filtered?"Limpar filtro":"+  Adicionar anime",!filtered);button.Under=Theme.Surface;button.SetBounds(Theme.S(28),Theme.S(146),Theme.S(186),Theme.S(42));
  button.Click+=delegate{if(filtered)filter.Clear();else AddAnime();};empty.Controls.Add(button);
  return empty;
 }
 Panel CreateCard(Anime anime){
  var card=new LinePanel{Width=Theme.S(400),Height=Theme.S(200),Margin=new Padding(0,0,Theme.S(14),Theme.S(14)),Tag=anime,Badge=Theme.StatusName(anime.Status),BadgeColor=Theme.StatusColor(anime.Status),YearText=anime.Year>0?anime.Year.ToString():"",Progress=anime.Total>0&&anime.Status!="planned"?Math.Min(1.0,(double)anime.Episode/anime.Total):-1};
  var picture=new CoverBox{Initial=Theme.Initial(anime.Title),Under=Theme.Surface};Theme.Place(picture,16,16,88,124);card.Controls.Add(picture);
  int left=Theme.S(116),width=card.Width-left-Theme.S(16);
  var title=Theme.Label(anime.Title,12,Theme.Text,FontStyle.Bold);title.AutoEllipsis=true;title.BackColor=Theme.Surface;title.SetBounds(left,Theme.S(46),width,Theme.S(46));title.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;card.Controls.Add(title);
  var progress=Theme.Label(Theme.Episodes(anime),10,Theme.Muted);progress.AutoEllipsis=true;progress.BackColor=Theme.Surface;progress.SetBounds(left,Theme.S(94),width,Theme.S(24));progress.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;card.Controls.Add(progress);
  var buttons=new FlowLayoutPanel{WrapContents=false,BackColor=Theme.Surface,Padding=new Padding(Theme.S(4),Theme.S(6),0,0),Margin=new Padding(0)};
  buttons.SetBounds(Theme.S(12),card.Height-Theme.S(54),card.Width-Theme.S(24),Theme.S(46));buttons.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;
  var action=Theme.Button(status=="watching"?"+1 episódio":status=="planned"?"Começar a assistir":"Voltar a assistir",status!="completed");action.Size=Theme.S(150,36);
  action.Click+=delegate{
   Anime changed=anime.Copy();
   if(anime.Status=="watching"){changed.Episode=Math.Min(100000,changed.Episode+1);if(changed.Total>0&&changed.Episode>=changed.Total){changed.Episode=changed.Total;changed.Status="completed";}}
   else{changed.Status="watching";if(anime.Status=="completed")changed.Episode=0;}
   changed.Validate();SaveEntry(changed,false);
  };
  var edit=Theme.Button("Editar");edit.Size=Theme.S(78,36);edit.Click+=delegate{Edit(anime);};
  var remove=Theme.Button("Remover");remove.Size=Theme.S(90,36);remove.Danger=true;remove.Click+=delegate{
   if(Notice.Ask(this,"Remover anime","\""+anime.Title+"\" será removido da sua biblioteca.","Remover","Cancelar",true))SaveList(entries.Where(a=>a.Id!=anime.Id).Select(a=>a.Copy()).ToList());
  };
  foreach(RoundButton b in new[]{action,edit,remove}){b.Under=Theme.Surface;b.Margin=new Padding(0,0,Theme.S(8),0);buttons.Controls.Add(b);}
  card.Controls.Add(buttons);
  if(!String.IsNullOrWhiteSpace(anime.Cover)){var ignored=Catalog.LoadCover(picture,anime);}
  return card;
 }
 void ResizeCards(){
  int available=Math.Max(Theme.S(360),cards.ClientSize.Width-(cards.VerticalScroll.Visible?0:SystemInformation.VerticalScrollBarWidth)-Theme.S(2));
  int gap=Theme.S(14),columns=Math.Max(1,Math.Min(3,available/(Theme.S(400)+gap)));
  foreach(Control card in cards.Controls)card.Width=card.Tag is string?available:available/columns-gap;
 }
 IWin32Window TopWindow(){Form top=Application.OpenForms.Cast<Form>().LastOrDefault(f=>f.Visible&&!(f is Notice));return top??this;}
 bool SaveList(List<Anime> next){
  try{store.Save(next);entries=next;feedback.Text="Salvo às "+DateTime.Now.ToString("HH:mm");Render(true);return true;}
  catch(Exception ex){Notice.Tell(TopWindow(),"Não foi possível salvar","As alterações anteriores continuam preservadas.\n\n"+ex.Message);return false;}
 }
 bool SaveEntry(Anime entry,bool isNew){
  if(isNew&&entries.Any(a=>(entry.CatalogId>0&&a.CatalogId==entry.CatalogId)||String.Equals(a.Title.Trim(),entry.Title.Trim(),StringComparison.CurrentCultureIgnoreCase))){
   Notice.Tell(TopWindow(),"Anime já adicionado","Esse anime já está na sua biblioteca. Use Editar para mudar a lista ou o episódio.");return false;
  }
  var next=entries.Select(a=>a.Copy()).ToList();if(isNew)next.Add(entry);else{int index=next.FindIndex(a=>a.Id==entry.Id);if(index<0)return false;next[index]=entry;}return SaveList(next);
 }
 void Edit(Anime entry){using(var editor=new EditorForm(entry,false,entry.Status,a=>SaveEntry(a,false)))editor.ShowDialog(this);}
 void AddAnime(){
  if(Application.OpenForms.OfType<SearchForm>().Any())return;
  var owned=new HashSet<int>(entries.Where(a=>a.CatalogId>0).Select(a=>a.CatalogId));
  using(var dialog=new SearchForm(delegate(SearchForm owner,Anime selected){
   using(var editor=new EditorForm(selected,true,status,a=>SaveEntry(a,true)))return editor.ShowDialog(owner)==DialogResult.OK;
  },owned))dialog.ShowDialog(this);
 }
}
static class Program {
 [STAThread]static int Main(string[] args){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  if(args.Length>0&&args[0]=="--self-test")return SelfTest();
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
  Application.ThreadException+=delegate(object s,ThreadExceptionEventArgs e){try{Notice.Tell(null,"Algo deu errado","O AniLista encontrou um problema, mas sua biblioteca continua salva.\n\n"+e.Exception.Message);}catch{}};
  bool ownsMutex;using(var mutex=new Mutex(true,"Local\\AniLista-Desktop-"+Environment.UserName,out ownsMutex)){
   if(!ownsMutex){Notice.Tell(null,"O AniLista já está aberto","Confira a barra de tarefas.");return 0;}
   try{var store=new LibraryStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AniLista"));Application.Run(new MainForm(store,store.Load()));return 0;}
   catch(Exception ex){Notice.Tell(null,"Não foi possível abrir o AniLista",ex.Message);return 1;}
   finally{mutex.ReleaseMutex();}
  }
 }
 static int SelfTest(){
  string folder=Path.Combine(Path.GetTempPath(),"AniLista-test-"+Guid.NewGuid().ToString("N"));
  try{
   var store=new LibraryStore(folder);Require(store.Load().Count==0,"Biblioteca inicial vazia");
   var anime=new Anime{Title="Teste japonês 日本語",Status="watching",Episode=3,Total=12};anime.Validate();store.Save(new List<Anime>{anime});var loaded=store.Load();
   Require(loaded.Count==1&&loaded[0].Title==anime.Title&&loaded[0].Episode==3,"Persistência Unicode e episódio");
   Anime changed=anime.Copy();changed.Episode=4;changed.Validate();store.Save(new List<Anime>{changed});Require(store.Load()[0].Episode==4&&File.Exists(store.FilePath+".bak"),"Atualização atômica e backup");
   changed.Status="completed";changed.Validate();Require(changed.Episode==12,"Concluído usa total");changed.Status="planned";changed.Validate();Require(changed.Episode==0,"Planejado começa em zero");
   changed.Status="watching";changed.Episode=13;bool rejected=false;try{changed.Validate();}catch{rejected=true;}Require(rejected,"Episódio acima do total rejeitado");
   File.WriteAllText(store.FilePath,"corrompido");Require(store.Load()[0].Episode==3&&store.Recovered,"Recuperação de backup");store.Save(new List<Anime>());Require(store.Load().Count==0,"Remoção persistida");
   List<SearchResult> catalog=Catalog.Search("Naruto",CancellationToken.None).GetAwaiter().GetResult();Require(catalog.Any(a=>a.Anime.Title.ToUpper().Contains("NARUTO")),"Busca real no catálogo");
   Require(catalog.Any(a=>a.Thumb.Length>0),"Resultados trazem o ícone da capa");
   Image thumb=Catalog.LoadImage(catalog.First(a=>a.Thumb.Length>0).Thumb,CancellationToken.None).GetAwaiter().GetResult();Require(thumb!=null&&thumb.Width>0,"Download do ícone da capa");thumb.Dispose();
   Console.WriteLine("PASS: persistência, atualização, backup, recuperação, listas, episódios, busca no catálogo e ícones das capas.");return 0;
  }catch(Exception ex){Console.WriteLine("FAIL: "+ex);return 1;}
 }
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
}
