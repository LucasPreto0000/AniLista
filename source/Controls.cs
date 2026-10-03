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
public sealed class RoundButton:Button {
 public bool Primary,Nav,Danger,Ellipsis;public Color Dot=Color.Empty,Under=Theme.Background;bool hover,down;
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
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(this,Nav?10:8))){using(var b=new SolidBrush(fill))g.FillPath(b,path);using(var pen=new Pen(border))g.DrawPath(pen,path);}
  if(Nav){
   if(Dot.A>0)using(var b=new SolidBrush(Dot))g.FillEllipse(b,Theme.S(this,16),Height/2-Theme.S(this,4),Theme.S(this,8),Theme.S(this,8));
   int split=Text.LastIndexOf("   ");string name=split>0?Text.Substring(0,split):Text,count=split>0?Text.Substring(split+3):"";
   TextRenderer.DrawText(g,name,Font,new Rectangle(Theme.S(this,34),0,Width-Theme.S(this,86),Height),fore,Theme.Line);
   if(count.Length>0){
    var pill=new Rectangle(Width-Theme.S(this,50),Height/2-Theme.S(this,11),Theme.S(this,36),Theme.S(this,22));
    using(var p=Theme.Round(pill,pill.Height/2))using(var b=new SolidBrush(selected?Color.FromArgb(90,Theme.Accent):Color.FromArgb(34,36,50)))g.FillPath(b,p);
    TextRenderer.DrawText(g,count,Font,pill,fore,Theme.Center);
   }
  }else if(Ellipsis){float size=3*Theme.ScaleFor(this),gap=4*Theme.ScaleFor(this),total=3*size+2*gap,left=(Width-total)/2f,top=(Height-size)/2f;using(var brush=new SolidBrush(fore))for(int i=0;i<3;i++)g.FillEllipse(brush,left+i*(size+gap),top,size,size);}
  else TextRenderer.DrawText(g,Text,Font,new Rectangle(Theme.S(this,4),0,Width-Theme.S(this,8),Height),fore,Theme.Center);
  if(Focused&&ShowFocusCues)using(var p=Theme.Round(new Rectangle(2,2,Width-5,Height-5),Theme.S(this,7)))using(var pen=new Pen(Color.FromArgb(150,Primary?Color.White:Theme.Accent),1.5f))g.DrawPath(pen,p);
 }
}
public sealed class InputBox:Panel {
 public bool Active,Icon;
 public InputBox(){DoubleBuffered=true;BackColor=Theme.Input;ResizeRedraw=true;Cursor=Cursors.IBeam;}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent!=null?Parent.BackColor:Theme.Background);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(this,9)))using(var b=new SolidBrush(Theme.Input))using(var pen=new Pen(Active?Theme.Accent:Theme.Border,Active?1.6f:1f)){g.FillPath(b,path);g.DrawPath(pen,path);}
  if(Icon)using(var pen=new Pen(Active?Theme.Accent:Theme.Muted,1.8f*Theme.ScaleFor(this)){StartCap=LineCap.Round,EndCap=LineCap.Round}){
   int cx=Theme.S(this,15),cy=Height/2-Theme.S(this,8),d=Theme.S(this,12);g.DrawEllipse(pen,cx,cy,d,d);
   g.DrawLine(pen,cx+d-Theme.S(this,2),cy+d-Theme.S(this,2),cx+d+Theme.S(this,4),cy+d+Theme.S(this,4));
  }
 }
 protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);foreach(Control c in Controls)if(c is TextBox){c.Top=(Height-c.PreferredSize.Height)/2;}}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);foreach(Control c in Controls)if(c is TextBox){c.Focus();break;}}
}
public sealed class CoverBox:PictureBox {
 public string Initial="";public Color Under=Theme.Surface;public int Radius=8;
 public CoverBox(){DoubleBuffered=true;}
 protected override void OnPaintBackground(PaintEventArgs e){}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.Clear(Under);
  Theme.DrawCover(this,g,new Rectangle(0,0,Width-1,Height-1),Image,Initial,Theme.S(this,Radius));
 }
 protected override void Dispose(bool disposing){if(disposing&&Image!=null){Image old=Image;Image=null;old.Dispose();}base.Dispose(disposing);}
}
public sealed class Brand:Control {
 public Brand(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);BackColor=Color.Transparent;}
 protected override void OnPaint(PaintEventArgs e){
  Font f=Theme.Font(this,26,FontStyle.Bold);
  var s=TextRenderer.MeasureText(e.Graphics,"Ani",f,new Size(400,80),TextFormatFlags.NoPadding);
  TextRenderer.DrawText(e.Graphics,"Ani",f,new Point(0,0),Theme.Accent,TextFormatFlags.NoPadding);
  TextRenderer.DrawText(e.Graphics,"Lista",f,new Point(s.Width,0),Theme.Text,TextFormatFlags.NoPadding);
 }
}
public sealed class FlowPanel:FlowLayoutPanel { public FlowPanel(){DoubleBuffered=true;Theme.DarkScroll(this);} }
public class LinePanel:Panel {
 public string Badge,YearText="";public Color BadgeColor=Theme.Accent;public double Progress=-1;
 public LinePanel(){DoubleBuffered=true;BackColor=Theme.Surface;ResizeRedraw=true;}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent!=null?Parent.BackColor:Theme.Background);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(this,12))){using(var b=new SolidBrush(BackColor))g.FillPath(b,path);using(var pen=new Pen(Theme.Border))g.DrawPath(pen,path);}
  int left=Theme.S(this,118);
  if(Badge!=null){
   Rectangle chip;Theme.Chip(this,g,Badge.ToUpper(),BadgeColor,new Rectangle(left,Theme.S(this,18),0,Theme.S(this,22)),false,out chip);
   if(YearText.Length>0)TextRenderer.DrawText(g,YearText,Theme.Font(this,9),new Rectangle(chip.Right+Theme.S(this,10),chip.Y,Theme.S(this,60),chip.Height),Theme.Muted,Theme.Line);
  }
  if(Progress>=0){
   int y=Theme.S(this,126),h=Theme.S(this,6),w=Math.Max(Theme.S(this,20),Width-left-Theme.S(this,66));
   using(var p=Theme.Round(new Rectangle(left,y,w,h),h/2))using(var b=new SolidBrush(Theme.Border))g.FillPath(b,p);
   int fill=(int)(w*Progress);if(fill>=h)using(var p=Theme.Round(new Rectangle(left,y,fill,h),h/2))using(var b=new SolidBrush(BadgeColor))g.FillPath(b,p);
   TextRenderer.DrawText(g,(int)Math.Round(Progress*100)+"%",Theme.Font(this,9,FontStyle.Bold),new Rectangle(left+w+Theme.S(this,10),y-Theme.S(this,7),Theme.S(this,46),Theme.S(this,20)),Theme.Muted,Theme.Line);
  }
 }
}
// Seletor de lista com três opções, no lugar do ComboBox claro do Windows.
public sealed class Segmented:Control {
 readonly string[] options;readonly Color[] colors;int selected,hover=-1;
 public event EventHandler SelectedIndexChanged;
 public Segmented(string[] items,Color[] tints){
  options=items;colors=tints;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
  TabStop=true;Cursor=Cursors.Hand;Font=Theme.Font(this,10,FontStyle.Bold);
 }
 public int SelectedIndex{get{return selected;}set{int v=Math.Max(0,Math.Min(options.Length-1,value));if(v==selected)return;selected=v;Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
 Rectangle Segment(int i){int pad=Theme.S(this,4),w=(Width-2*pad)/options.Length,x=pad+i*w,right=i==options.Length-1?Width-pad:x+w;return new Rectangle(x,pad,right-x-1,Height-2*pad-1);}
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
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(this,9)))using(var b=new SolidBrush(Theme.Input))using(var pen=new Pen(Focused?Theme.Accent:Theme.Border,Focused?1.6f:1f)){g.FillPath(b,path);g.DrawPath(pen,path);}
  for(int i=0;i<options.Length;i++){
   Rectangle r=Segment(i);bool on=i==selected;Color tint=colors[i];
   if(on||i==hover)using(var p=Theme.Round(r,Theme.S(this,7))){
    using(var b=new SolidBrush(on?Color.FromArgb(46,tint):Color.FromArgb(42,44,61)))g.FillPath(b,p);
    if(on)using(var pen=new Pen(Color.FromArgb(170,tint)))g.DrawPath(pen,p);
   }
   int dot=Theme.S(this,8),gap=Theme.S(this,8);Size s=TextRenderer.MeasureText(g,options[i],Font,new Size(400,40),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
   int x=r.X+Math.Max(Theme.S(this,6),(r.Width-dot-gap-s.Width)/2);
   using(var b=new SolidBrush(on?tint:Color.FromArgb(130,tint)))g.FillEllipse(b,x,r.Y+r.Height/2-dot/2,dot,dot);
   TextRenderer.DrawText(g,options[i],Font,new Rectangle(x+dot+gap,r.Y,r.Right-x-dot-gap-Theme.S(this,4),r.Height),on||i==hover?Theme.Text:Theme.Muted,Theme.Line);
  }
 }
}
// Campo numérico escuro com botões − e +, no lugar do NumericUpDown claro do Windows.
public sealed class Stepper:Panel {
 readonly TextBox box;readonly RoundButton minus,plus;int value;bool locked,active,syncing;
 public int Maximum=100000;public event EventHandler ValueChanged;
 public Stepper(){
  DoubleBuffered=true;ResizeRedraw=true;BackColor=Theme.Input;
  box=Theme.TextBox(this);box.TextAlign=HorizontalAlignment.Center;box.Font=Theme.Font(this,12,FontStyle.Bold);box.MaxLength=6;box.Text="0";
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
 RoundButton Small(string text){return new RoundButton{Text=text,Font=Theme.Font(this,12,FontStyle.Bold),Under=Theme.Input,BackColor=Theme.Surface,ForeColor=Theme.Text,Cursor=Cursors.Hand,TabStop=false};}
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
  int pad=Theme.S(this,5),size=Math.Max(10,Height-2*pad);minus.SetBounds(pad,pad,size,size);plus.SetBounds(Width-pad-size,pad,size,size);
  int left=pad+size+Theme.S(this,6);box.SetBounds(left,(Height-box.PreferredHeight)/2,Math.Max(10,Width-2*left),box.PreferredHeight);
 }
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent!=null?Parent.BackColor:Theme.Background);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(this,9)))using(var b=new SolidBrush(locked?Theme.Locked:Theme.Input))using(var pen=new Pen(active&&!locked?Theme.Accent:Theme.Border,active&&!locked?1.6f:1f)){g.FillPath(b,path);g.DrawPath(pen,path);}
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(!locked)box.Focus();}
}
// Janela de aviso/confirmação no tema escuro, no lugar do MessageBox branco.
public sealed class Notice:DpiForm {
 Notice(string title,string message,string ok,string cancel,bool danger){
  Text="AniLista";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font(this,10);Theme.DarkTitle(this);
  FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowIcon=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  int width=Theme.S(this,470),pad=Theme.S(this,28),inner=width-2*pad;
  var bar=new Panel{BackColor=danger?Theme.Danger:Theme.Accent};bar.SetBounds(pad,Theme.S(this,27),Theme.S(this,4),Theme.S(this,26));Controls.Add(bar);
  var head=Theme.Label(this,title,15,Theme.Text,FontStyle.Bold);head.AutoEllipsis=true;head.SetBounds(pad+Theme.S(this,14),Theme.S(this,20),inner-Theme.S(this,14),Theme.S(this,38));Controls.Add(head);
  Font font=Theme.Font(this,10.5f);int height=TextRenderer.MeasureText(message,font,new Size(inner,10000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
  var body=Theme.Label(this,message,10.5f,Theme.Soft);body.SetBounds(pad,Theme.S(this,70),inner,height+Theme.S(this,8));Controls.Add(body);
  int top=body.Bottom+Theme.S(this,24);
  var confirm=Theme.Button(this,ok,true);confirm.Danger=danger;confirm.DialogResult=DialogResult.OK;
  int cw=Math.Max(Theme.S(this,112),TextRenderer.MeasureText(ok,confirm.Font).Width+Theme.S(this,40));confirm.SetBounds(width-pad-cw,top,cw,Theme.S(this,40));Controls.Add(confirm);AcceptButton=confirm;
  if(cancel!=null){
   var back=Theme.Button(this,cancel);back.DialogResult=DialogResult.Cancel;int bw=Math.Max(Theme.S(this,112),TextRenderer.MeasureText(cancel,back.Font).Width+Theme.S(this,40));
   back.SetBounds(confirm.Left-Theme.S(this,10)-bw,top,bw,Theme.S(this,40));Controls.Add(back);CancelButton=back;if(danger)ActiveControl=back;
  }else CancelButton=confirm;
  ClientSize=new Size(width,top+Theme.S(this,40)+Theme.S(this,24));
 }
 public static bool Ask(IWin32Window owner,string title,string message,string ok,string cancel,bool danger=false){
  using(var n=new Notice(title,message,ok,cancel,danger)){
   if(owner==null){n.StartPosition=FormStartPosition.CenterScreen;n.ShowInTaskbar=true;n.ShowIcon=Theme.AppIcon!=null;if(Theme.AppIcon!=null)n.Icon=Theme.AppIcon;}
   return n.ShowDialog(owner)==DialogResult.OK;
  }
 }
 public static void Tell(IWin32Window owner,string title,string message){Ask(owner,title,message,"OK",null);}
}
}
