using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace AniLista {
public sealed class AnitsuResultsForm:DpiForm {
 public AnitsuCandidate Selected{get;private set;}
 public AnitsuResultsForm(IList<AnitsuCandidate> candidates){
  Text="Escolher pasta no Anitsu";BackColor=Theme.Background;ForeColor=Theme.Text;Size=Theme.S(this,760,430);MinimumSize=Theme.S(this,600,360);StartPosition=FormStartPosition.CenterParent;Theme.DarkTitle(this);
  var hint=Theme.Label(this,"Confira a temporada e o caminho antes de abrir.",11,Theme.Muted);hint.Dock=DockStyle.Top;hint.Height=48;hint.Padding=new Padding(12);
  var list=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Input,ForeColor=Theme.Text,Font=Theme.Font(this,11),HorizontalScrollbar=true,BorderStyle=BorderStyle.None};foreach(var candidate in candidates)list.Items.Add(candidate);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=60,Padding=new Padding(10),FlowDirection=FlowDirection.RightToLeft};
  var open=Theme.Button(this,"Abrir no Anitsu",true);open.Width=160;open.Enabled=false;open.Click+=delegate{Selected=list.SelectedItem as AnitsuCandidate;DialogResult=DialogResult.OK;Close();};
  var copy=Theme.Button(this,"Copiar caminho");copy.Width=150;copy.Enabled=false;copy.Click+=delegate{var candidate=list.SelectedItem as AnitsuCandidate;if(candidate!=null)Clipboard.SetText(candidate.Path);};
  var cancel=Theme.Button(this,"Cancelar");cancel.Width=110;cancel.DialogResult=DialogResult.Cancel;list.SelectedIndexChanged+=delegate{open.Enabled=copy.Enabled=list.SelectedItem!=null;};actions.Controls.Add(open);actions.Controls.Add(copy);actions.Controls.Add(cancel);
  Controls.Add(list);Controls.Add(hint);Controls.Add(actions);CancelButton=cancel;
 }
}
}
