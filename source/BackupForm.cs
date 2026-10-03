using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AniLista {
public sealed class BackupForm:DpiForm {
 readonly LibraryStore store;readonly Func<List<Anime>,int> recover;readonly ListBox copies;readonly RoundButton restore;readonly Label preview;
 public BackupForm(LibraryStore library,Func<List<Anime>,int> onRecover){
  store=library;recover=onRecover;
  Text="Backup e recuperação · AniLista";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font(this,10);Theme.DarkTitle(this);
  StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;ClientSize=Theme.S(this,720,480);MinimumSize=SizeFromClientSize(Theme.S(this,620,440));
  var title=Theme.Label(this,"Seus animes protegidos",20,Theme.Text,FontStyle.Bold);Theme.Place(this,title,24,18,670,36);title.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(title);
  var hint=Theme.Label(this,"Recupere animes de uma cópia sem substituir os episódios e listas atuais.",10,Theme.Muted);Theme.Place(this,hint,24,60,670,36);hint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(hint);
  copies=new ListBox{BackColor=Theme.Surface,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,Font=Theme.Font(this,11),IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=Theme.S(this,32)};Theme.Place(this,copies,24,108,672,180);copies.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;Theme.DarkScroll(copies);Controls.Add(copies);
  copies.DrawItem+=delegate(object sender,DrawItemEventArgs args){if(args.Index<0)return;using(var brush=new SolidBrush((args.State&DrawItemState.Selected)!=0?Theme.Selected:Theme.Surface))args.Graphics.FillRectangle(brush,args.Bounds);TextRenderer.DrawText(args.Graphics,copies.Items[args.Index].ToString(),copies.Font,new Rectangle(args.Bounds.X+Theme.S(this,10),args.Bounds.Y,args.Bounds.Width-Theme.S(this,20),args.Bounds.Height),Theme.Text,Theme.Line);};
  LayoutScaleChanged+=delegate{copies.ItemHeight=Theme.S(this,32);};
  preview=Theme.Label(this,"Selecione uma cópia para ver os animes.",9,Theme.Soft);preview.AutoEllipsis=true;Theme.Place(this,preview,24,296,672,44);preview.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;Controls.Add(preview);
  var location=Theme.Label(this,"Backup automático: "+store.BackupPath(0),9,Theme.Muted);location.AutoEllipsis=true;Theme.Place(this,location,24,348,672,24);location.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;Controls.Add(location);
  var export=Theme.Button(this,"Exportar biblioteca");Theme.Place(this,export,24,384,186,40);export.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;export.Click+=delegate{Export();};Controls.Add(export);
  var import=Theme.Button(this,"Abrir outra cópia");Theme.Place(this,import,220,384,180,40);import.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;import.Click+=delegate{Import();};Controls.Add(import);
  restore=Theme.Button(this,"Recuperar animes",true);Theme.Place(this,restore,510,384,186,40);restore.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;restore.Enabled=false;restore.Click+=delegate{var selected=copies.SelectedItem as LibraryStore.BackupInfo;if(selected!=null)Recover(selected.Path);};Controls.Add(restore);
  copies.SelectedIndexChanged+=delegate{restore.Enabled=copies.SelectedItem!=null;var selected=copies.SelectedItem as LibraryStore.BackupInfo;if(selected==null)return;try{var entries=store.ReadBackup(selected.Path);preview.Text=entries.Count==0?"Esta cópia está vazia.":String.Join(" · ",entries.Select(a=>a.Title+" ("+Theme.StatusName(a.Status)+", "+Theme.Episodes(a)+")"));}catch{preview.Text="Não foi possível ler esta cópia.";restore.Enabled=false;}};
  var foot=Theme.Label(this,"Um único backup automático, atualizado a cada salvamento e guardado no AppData.",9,Theme.Muted);Theme.Place(this,foot,24,438,672,24);foot.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;Controls.Add(foot);
  Reload();
 }
 void Reload(){copies.Items.Clear();foreach(var copy in store.GetBackups())copies.Items.Add(copy);if(copies.Items.Count>0)copies.SelectedIndex=0;}
 void Export(){using(var dialog=new SaveFileDialog{Title="Exportar biblioteca",Filter="Biblioteca JSON (*.json)|*.json",FileName="AniLista-biblioteca-"+DateTime.Now.ToString("yyyy-MM-dd")+".json",OverwritePrompt=true}){
  if(dialog.ShowDialog(this)!=DialogResult.OK)return;
  try{store.Export(dialog.FileName);Notice.Tell(this,"Cópia exportada","Sua biblioteca foi copiada para:\n"+dialog.FileName);}catch(Exception ex){Notice.Tell(this,"Não foi possível exportar",ex.Message);}
 }}
 void Import(){using(var dialog=new OpenFileDialog{Title="Escolher cópia da biblioteca",Filter="Biblioteca JSON (*.json;*.bak;*.bak.*)|*.json;*.bak;*.bak.*|Todos os arquivos (*.*)|*.*"}){if(dialog.ShowDialog(this)==DialogResult.OK)Recover(dialog.FileName);}}
 void Recover(string path){try{
  int added=recover(store.ReadBackup(path));if(added<0)return;
  Notice.Tell(this,added==0?"Biblioteca já contém estes animes":"Animes recuperados",added==0?"Não há animes novos nesta cópia.":added+" anime(s) recuperado(s). Seus episódios e listas atuais foram preservados.");Reload();
 }catch(Exception ex){Notice.Tell(this,"Não foi possível recuperar",ex.Message);}}
}
}
