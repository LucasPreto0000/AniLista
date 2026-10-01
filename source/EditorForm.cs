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
public sealed class EditorForm:DpiForm {
 readonly Anime entry;readonly Func<Anime,bool> save;
 TextBox title;Segmented status;Stepper episode,total;Label error;CoverBox cover;bool updating;
 public EditorForm(Anime anime,bool isNew,string initialStatus,Func<Anime,bool> onSave){
  entry=anime.Copy();save=onSave;if(isNew)entry.Status=initialStatus=="watching"||initialStatus=="completed"?initialStatus:"planned";
  Text=isNew?"Adicionar anime · AniLista":"Editar anime · AniLista";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font(this,10);Theme.DarkTitle(this);
  FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowIcon=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;ClientSize=Theme.S(this,640,472);
  var heading=Theme.Label(this,isNew?"Adicionar à sua biblioteca":"Editar anime",19,Theme.Text,FontStyle.Bold);Theme.Place(this,heading,26,18,588,40);Controls.Add(heading);
  var sub=Theme.Label(this,!isNew?"Atualize a lista ou o seu progresso.":entry.CatalogId>0?"Confira os dados do catálogo e escolha a lista.":"Preencha os dados do anime que você quer guardar.",10,Theme.Muted);Theme.Place(this,sub,28,58,584,24);Controls.Add(sub);
  cover=new CoverBox{Initial=Theme.Initial(entry.Title),Under=Theme.Background};Theme.Place(this,cover,28,100,120,170);Controls.Add(cover);
  if(entry.CatalogId>0){var ignored=Catalog.LoadCover(cover,entry);}
  if(entry.Year>0){var year=Theme.Label(this,"Lançamento: "+entry.Year,9,Theme.Muted);year.TextAlign=ContentAlignment.MiddleCenter;Theme.Place(this,year,28,276,120,22);Controls.Add(year);}
  AddLabel("Nome do anime",172,96,440);title=Theme.TextBox(this);title.Text=entry.Title;title.MaxLength=180;Controls.Add(Theme.Wrap(this,title,172,122,440,42));
  title.TextChanged+=delegate{cover.Initial=Theme.Initial(title.Text);cover.Invalidate();};
  AddLabel("Lista",172,176,440);
  status=new Segmented(new[]{"Quero assistir","Assistindo","Concluídos"},new[]{Theme.Warn,Theme.Accent,Theme.Success});Theme.Place(this,status,172,202,440,42);
  status.SelectedIndex=entry.Status=="watching"?1:entry.Status=="completed"?2:0;Controls.Add(status);
  AddLabel("Episódio atual",172,258,210);AddLabel("Total de episódios",394,258,218);
  episode=new Stepper();Theme.Place(this,episode,172,284,210,42);episode.Value=entry.Episode;Controls.Add(episode);
  total=new Stepper();Theme.Place(this,total,394,284,218,42);total.Value=entry.Total;Controls.Add(total);
  var hint=Theme.Label(this,"0 = ainda não informado",9,Theme.Muted);Theme.Place(this,hint,396,330,216,20);Controls.Add(hint);
  error=Theme.Label(this,"",10,Theme.Danger);Theme.Place(this,error,28,356,584,42);Controls.Add(error);
  var cancel=Theme.Button(this,"Cancelar");Theme.Place(this,cancel,316,408,112,42);cancel.DialogResult=DialogResult.Cancel;
  var confirm=Theme.Button(this,isNew?"Adicionar anime":"Salvar alterações",true);Theme.Place(this,confirm,440,408,172,42);
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
 void AddLabel(string text,int x,int y,int w){var label=Theme.Label(this,text,10,Theme.Muted);Theme.Place(this,label,x,y,w,24);Controls.Add(label);}
 void UpdateNumbers(){
  if(updating)return;updating=true;int s=status.SelectedIndex;
  episode.Locked=!(s==1||(s==2&&total.Value==0));
  if(s==0)episode.Value=0;else if(s==2&&total.Value>0)episode.Value=total.Value;
  updating=false;
 }
}
}
