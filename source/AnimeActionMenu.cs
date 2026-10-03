using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AniLista {
// Um painel da própria janela evita moldura e sombra nativas retangulares.
public sealed class AnimeActionMenu:Panel,IMessageFilter {
 readonly RoundButton search;Form owner;Control anchor;bool listening;
 public AnimeActionMenu(Action action){
  DoubleBuffered=true;BackColor=Theme.Surface;Visible=false;
  search=Theme.Button(this,"Pesquisar no Anitsu");search.Under=Theme.Surface;
  search.Cursor=AppCursors.Hand;search.ForeColor=Theme.Text;
  search.MouseEnter+=delegate{search.ForeColor=Theme.Accent;};search.MouseLeave+=delegate{search.ForeColor=Theme.Text;};
  search.Click+=delegate{Dismiss();action();};Controls.Add(search);
 }
 public void Toggle(Control source){
  if(Visible){Dismiss();return;}
  owner=source.FindForm();if(owner==null)return;anchor=source;
  Size=Theme.S(source,210,54);int pad=Theme.S(source,6);
  search.Font=Theme.Font(source,10);search.SetBounds(pad,pad,Width-pad*2,Height-pad*2);
  using(var path=Theme.Round(new Rectangle(0,0,Width,Height),Theme.S(source,12))){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}
  owner.Controls.Add(this);
  Point point=owner.PointToClient(source.PointToScreen(new Point(source.Width,source.Height+Theme.S(source,6))));
  int x=Math.Max(0,Math.Min(owner.ClientSize.Width-Width,point.X-Width));
  int y=point.Y;if(y+Height>owner.ClientSize.Height)y=owner.PointToClient(source.PointToScreen(Point.Empty)).Y-Height-Theme.S(source,6);
  Location=new Point(x,Math.Max(0,y));BringToFront();Show();search.Focus();
  owner.Deactivate+=OwnerChanged;owner.Resize+=OwnerChanged;
  Application.AddMessageFilter(this);listening=true;
 }
 void OwnerChanged(object sender,EventArgs e){Dismiss();}
 public void Dismiss(){
  Hide();if(listening){Application.RemoveMessageFilter(this);listening=false;}
  if(owner!=null){owner.Deactivate-=OwnerChanged;owner.Resize-=OwnerChanged;owner.Controls.Remove(this);owner=null;}
 }
 public bool PreFilterMessage(ref Message message){
  if(!Visible)return false;
  if(message.Msg==0x100&&message.WParam.ToInt32()==(int)Keys.Escape){Dismiss();if(anchor!=null&&!anchor.IsDisposed)anchor.Focus();return true;}
  if(message.Msg==0x201||message.Msg==0x204||message.Msg==0x207||message.Msg==0x20A){
   Point mouse=Control.MousePosition;
   if(message.Msg==0x20A||(!RectangleToScreen(ClientRectangle).Contains(mouse)&&(anchor==null||!anchor.RectangleToScreen(anchor.ClientRectangle).Contains(mouse))))Dismiss();
  }
  return false;
 }
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  using(var path=Theme.Round(new Rectangle(0,0,Width-1,Height-1),Theme.S(anchor??this,12)))using(var pen=new Pen(Theme.Border))e.Graphics.DrawPath(pen,path);
 }
 protected override void Dispose(bool disposing){if(disposing)Dismiss();base.Dispose(disposing);}
}
}
