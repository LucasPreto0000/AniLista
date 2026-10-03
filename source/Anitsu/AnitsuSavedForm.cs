using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace AniLista {
public sealed class AnitsuSavedForm:DpiForm {
 public Anime Selected{get;private set;}
 public AnitsuSavedForm(IEnumerable<Anime> entries){
  Text="Buscar anime salvo no Anitsu";BackColor=Theme.Background;ForeColor=Theme.Text;Size=Theme.S(this,600,440);StartPosition=FormStartPosition.CenterParent;Theme.DarkTitle(this);
  var sorted=entries.OrderBy(a=>a.Title,StringComparer.CurrentCultureIgnoreCase).ToList();
  var list=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Input,ForeColor=Theme.Text,Font=Theme.Font(this,11),BorderStyle=BorderStyle.None};foreach(var anime in sorted)list.Items.Add(anime.Title);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=60,Padding=new Padding(10),FlowDirection=FlowDirection.RightToLeft};var search=Theme.Button(this,"Buscar no Anitsu",true);search.Width=180;search.Enabled=false;search.Click+=delegate{if(list.SelectedIndex>=0){Selected=sorted[list.SelectedIndex];DialogResult=DialogResult.OK;Close();}};list.SelectedIndexChanged+=delegate{search.Enabled=list.SelectedIndex>=0;};actions.Controls.Add(search);Controls.Add(list);Controls.Add(actions);
 }
}
}
