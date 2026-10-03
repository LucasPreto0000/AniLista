using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AniLista {
public sealed class AnimeCard:LinePanel {
 Anime anime;readonly CoverService covers;
 readonly CoverBox picture;
 readonly Label title,progress;
 readonly RoundButton action;
 readonly RoundButton more;readonly AnimeActionMenu options;
 CancellationTokenSource coverRequest;
 bool coverLoaded,coverVisible;
 public readonly float InitialScale;
 public AnimeCard(Anime initial,Action<Anime> save,Action<Anime> edit,Action<Anime> remove,float scale=1,CoverService service=null,Action<Anime> searchAnitsu=null){
  covers=service??CoverService.Shared;
  InitialScale=scale;
  Width=Theme.S(this,400);Height=Theme.S(this,200);Margin=new Padding(0,0,Theme.S(this,14),Theme.S(this,14));
  options=new AnimeActionMenu(delegate{if(searchAnitsu!=null)searchAnitsu(anime.Copy());});
  more=Theme.Button(this,"⋯");more.Ellipsis=true;more.Under=Theme.Surface;more.Font=Theme.Font(this,17,FontStyle.Bold);more.ForeColor=Theme.Muted;more.Anchor=AnchorStyles.Top|AnchorStyles.Right;more.Enabled=searchAnitsu!=null;more.SetBounds(Width-Theme.S(this,48),Theme.S(this,12),Theme.S(this,32),Theme.S(this,30));more.Click+=delegate{options.Toggle(more);};Controls.Add(more);
  picture=new CoverBox{Under=Theme.Surface};Theme.Place(this,picture,16,16,88,124);Controls.Add(picture);
  int left=Theme.S(this,116),width=Width-left-Theme.S(this,16);
  title=Theme.Label(this,"",12,Theme.Text,FontStyle.Bold);title.AutoEllipsis=true;title.BackColor=Theme.Surface;title.SetBounds(left,Theme.S(this,46),width,Theme.S(this,46));title.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(title);
  progress=Theme.Label(this,"",10,Theme.Muted);progress.AutoEllipsis=true;progress.BackColor=Theme.Surface;progress.SetBounds(left,Theme.S(this,94),width,Theme.S(this,24));progress.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(progress);
  var buttons=new FlowLayoutPanel{WrapContents=false,BackColor=Theme.Surface,Padding=new Padding(Theme.S(this,4),Theme.S(this,6),0,0),Margin=new Padding(0)};
  buttons.SetBounds(Theme.S(this,12),Height-Theme.S(this,54),Width-Theme.S(this,24),Theme.S(this,46));buttons.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;
  action=Theme.Button(this,"");action.Size=Theme.S(this,150,36);
  action.Click+=delegate{
   Anime changed=anime.Copy();
   if(changed.Status=="watching"){changed.Episode=Math.Min(100000,changed.Episode+1);if(changed.Total>0&&changed.Episode>=changed.Total){changed.Episode=changed.Total;changed.Status="completed";}}
   else{if(changed.Status=="completed")changed.Episode=0;changed.Status="watching";}
   save(changed);
  };
  var editButton=Theme.Button(this,"Editar");editButton.Size=Theme.S(this,78,36);editButton.Click+=delegate{edit(anime);};
  var removeButton=Theme.Button(this,"Remover");removeButton.Size=Theme.S(this,90,36);removeButton.Danger=true;removeButton.Click+=delegate{remove(anime);};
  foreach(var b in new[]{action,editButton,removeButton}){b.Under=Theme.Surface;b.Margin=new Padding(0,0,Theme.S(this,8),0);buttons.Controls.Add(b);}
  Controls.Add(buttons);Bind(initial);
  Resize+=delegate{
   bool compact=Width<Theme.S(this,400);action.Width=Theme.S(this,compact?134:150);editButton.Width=Theme.S(this,compact?70:78);removeButton.Width=Theme.S(this,compact?82:90);
  };
 }
 public void Bind(Anime value){
  if(anime!=null&&anime.Title==value.Title&&anime.Status==value.Status&&anime.Episode==value.Episode&&anime.Total==value.Total&&anime.Year==value.Year&&anime.CatalogId==value.CatalogId&&anime.Cover==value.Cover){anime=value;Tag=value;return;}
  bool newCover=anime==null||anime.CatalogId!=value.CatalogId||anime.Cover!=value.Cover;
  anime=value;Tag=value;
  more.AccessibleName="Mais opções de "+anime.Title;
  title.Text=anime.Title;progress.Text=Theme.Episodes(anime);picture.Initial=Theme.Initial(anime.Title);
  Badge=Theme.StatusName(anime.Status);BadgeColor=Theme.StatusColor(anime.Status);YearText=anime.Year>0?anime.Year.ToString():"";
  Progress=anime.Total>0&&anime.Status!="planned"?Math.Min(1.0,(double)anime.Episode/anime.Total):-1;
  action.Text=anime.Status=="watching"?"+1 episódio":anime.Status=="planned"?"Começar a assistir":"Voltar a assistir";
  action.Primary=anime.Status!="completed";action.ForeColor=action.Primary?Color.FromArgb(15,12,25):Theme.Text;
  if(newCover){CancelCover();coverLoaded=false;if(picture.Image!=null){picture.Image.Dispose();picture.Image=null;}}
  picture.Invalidate();action.Invalidate();Invalidate();
  if(newCover&&coverVisible)SetCoverVisible(true);
 }
 public void SetCoverVisible(bool visible){
  coverVisible=visible;
  if(!visible){CancelCover();return;}
  if(coverLoaded||coverRequest!=null||anime.CatalogId<=0||String.IsNullOrWhiteSpace(anime.Cover))return;
  coverRequest=new CancellationTokenSource();var request=coverRequest;var token=request.Token;var entry=anime.Copy();
  var ignored=LoadCoverAsync(entry,request,token);
 }
 async Task LoadCoverAsync(Anime entry,CancellationTokenSource request,CancellationToken token){
  Image image=null;
  try{
   image=await covers.LoadCoverAsync(entry,token);
   if(IsDisposed||token.IsCancellationRequested||request!=coverRequest||!coverVisible)return;
   if(image!=null){Image old=picture.Image;picture.Image=image;image=null;if(old!=null)old.Dispose();coverLoaded=true;picture.Invalidate();}
  }catch(OperationCanceledException){}finally{
   if(image!=null)image.Dispose();if(coverRequest==request)coverRequest=null;request.Dispose();
  }
 }
 void CancelCover(){if(coverRequest!=null){coverRequest.Cancel();coverRequest=null;}}
 protected override void Dispose(bool disposing){if(disposing){CancelCover();options.Dispose();}base.Dispose(disposing);}
}
}
