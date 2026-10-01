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
public sealed class SearchForm:DpiForm {
 readonly Func<SearchForm,Anime,bool> choose;readonly ICollection<int> owned;
 TextBox search;RoundButton searchButton,selectButton,moreButton;ListBox results;Label message,placeholder;
 System.Windows.Forms.Timer typing;string lastQuery="",loadedQuery="";int page;bool hasMore,busy;readonly ICatalogClient catalog;
 readonly List<SearchResult> items=new List<SearchResult>();CancellationTokenSource cancellation;int hover=-1;
 public SearchForm(Func<SearchForm,Anime,bool> onChoose,ICollection<int> library=null,ICatalogClient client=null){
  catalog=client??Catalog.Client;
  choose=onChoose;owned=library??new List<int>();
  Text="Adicionar anime · AniLista";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font(this,10);Theme.DarkTitle(this);AutoScaleMode=AutoScaleMode.None;
  StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;ClientSize=Theme.S(this,780,640);MinimumSize=SizeFromClientSize(Theme.S(this,640,520));
  if(Theme.AppIcon!=null)Icon=Theme.AppIcon;
  var heading=Theme.Label(this,"Encontre seu próximo anime",21,Theme.Text,FontStyle.Bold);Theme.Place(this,heading,26,18,724,46);heading.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(heading);
  var hint=Theme.Label(this,"Busque pelo nome em português, inglês ou japonês — ou cadastre manualmente.",10,Theme.Muted);Theme.Place(this,hint,28,64,724,24);hint.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(hint);
  search=Theme.TextBox(this);search.MaxLength=100;Theme.Cue(search,"Digite o nome do anime");Controls.Add(Theme.Wrap(this,search,28,100,588,44,AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,true));
  searchButton=Theme.Button(this,"Buscar",true);Theme.Place(this,searchButton,630,100,122,44);searchButton.Anchor=AnchorStyles.Top|AnchorStyles.Right;searchButton.Click+=async delegate{typing.Stop();await RunSearch(false,false);};Controls.Add(searchButton);AcceptButton=searchButton;
  // Busca ao vivo: depois de uma pequena pausa na digitação, os resultados aparecem sozinhos.
  typing=new System.Windows.Forms.Timer{Interval=380};
  typing.Tick+=async delegate{typing.Stop();await RunSearch(true,false);};
  search.TextChanged+=delegate{typing.Stop();ResetResults();if(search.Text.Trim().Length>=2){SetMessage("Aguardando a digitação...",false);typing.Start();}};
  search.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Down&&results.Items.Count>0){e.Handled=e.SuppressKeyPress=true;results.Focus();if(results.SelectedIndex<0)results.SelectedIndex=0;}};
  message=Theme.Label(this,"Digite o nome de um anime para começar.",10,Theme.Muted);message.AutoEllipsis=true;Theme.Place(this,message,28,154,724,26);message.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(message);
  var frame=new LinePanel{Padding=new Padding(Theme.S(this,6))};Theme.Place(this,frame,28,188,724,368);frame.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(frame);
  results=new ListBox{DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=Math.Min(255,Theme.S(this,84)),BorderStyle=BorderStyle.None,BackColor=Theme.Surface,ForeColor=Theme.Text,IntegralHeight=false,Dock=DockStyle.Fill,Font=Theme.Font(this,11)};
  Theme.DarkScroll(results);frame.Controls.Add(results);
  placeholder=Theme.Label(this,"As capas e os detalhes dos animes aparecem aqui.",10.5f,Theme.Muted);placeholder.BackColor=Theme.Surface;placeholder.TextAlign=ContentAlignment.MiddleCenter;placeholder.Dock=DockStyle.Fill;frame.Controls.Add(placeholder);placeholder.BringToFront();
  results.DrawItem+=DrawResult;LayoutScaleChanged+=delegate{results.ItemHeight=Math.Min(255,Theme.S(this,84));results.Invalidate();};
  results.MouseMove+=delegate(object s,MouseEventArgs e){int i=results.IndexFromPoint(e.Location);if(i!=hover){int old=hover;hover=i;Repaint(old);Repaint(hover);}};
  results.MouseLeave+=delegate{int old=hover;hover=-1;Repaint(old);};
  results.SelectedIndexChanged+=delegate{selectButton.Enabled=!busy&&loadedQuery==search.Text.Trim()&&results.SelectedIndex>=0;};
  results.DoubleClick+=delegate{if(results.IndexFromPoint(results.PointToClient(Cursor.Position))>=0)SelectResult();};
  results.Enter+=delegate{AcceptButton=selectButton;};search.Enter+=delegate{AcceptButton=searchButton;};
  results.Resize+=delegate{results.Invalidate();};
  moreButton=Theme.Button(this,"Carregar mais");Theme.Place(this,moreButton,28,576,170,44);moreButton.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;moreButton.Enabled=false;moreButton.Click+=async delegate{await RunSearch(false,true);};Controls.Add(moreButton);
  var manual=Theme.Button(this,"Cadastro manual");Theme.Place(this,manual,404,576,170,44);manual.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;manual.Click+=delegate{Choose(new Anime());};Controls.Add(manual);
  selectButton=Theme.Button(this,"Selecionar anime",true);Theme.Place(this,selectButton,586,576,166,44);selectButton.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;selectButton.Enabled=false;selectButton.Click+=delegate{SelectResult();};Controls.Add(selectButton);
  FormClosed+=delegate{typing.Stop();typing.Dispose();CancelRequest();ClearItems();};Shown+=delegate{search.Focus();};
 }
 void CancelRequest(){if(cancellation!=null){cancellation.Cancel();cancellation.Dispose();cancellation=null;}}
 void Repaint(int index){if(index>=0&&index<results.Items.Count)results.Invalidate(results.GetItemRectangle(index));}
 void Choose(Anime anime){if(choose(this,anime)){DialogResult=DialogResult.OK;Close();}}
 void SelectResult(){int i=results.SelectedIndex;if(!busy&&loadedQuery==search.Text.Trim()&&i>=0&&i<items.Count)Choose(items[i].Anime.Copy());}
 void ClearItems(){
  if(!results.IsDisposed)results.Items.Clear();
  foreach(SearchResult r in items)if(r.Image!=null){r.Image.Dispose();r.Image=null;}
  items.Clear();hover=-1;
 }
 void SetMessage(string text,bool warning){message.ForeColor=warning?Theme.Warn:Theme.Muted;message.Text=text;}
 void ResetResults(){
  CancelRequest();
  lastQuery=loadedQuery="";page=0;hasMore=false;busy=false;moreButton.Enabled=false;results.Enabled=false;results.BeginUpdate();ClearItems();results.EndUpdate();
  placeholder.Text="As capas e os detalhes dos animes aparecem aqui.";placeholder.Visible=true;
  searchButton.Enabled=true;selectButton.Enabled=false;
  SetMessage(search.Text.Trim().Length==0?"Digite o nome de um anime para começar.":"Continue digitando — os resultados aparecem sozinhos.",false);
 }
 async Task RunSearch(bool live,bool append){
  string text=search.Text.Trim();
  if(text.Length<2){if(live)ResetResults();else SetMessage("Digite pelo menos 2 caracteres.",true);return;}
  if(append&&(busy||!hasMore||loadedQuery!=text))return;
  if(live&&text==lastQuery)return;lastQuery=text;
  CancelRequest();
  var request=new CancellationTokenSource();var requestToken=request.Token;cancellation=request;
  busy=true;results.Enabled=false;searchButton.Enabled=false;selectButton.Enabled=false;moreButton.Enabled=false;
  if(!append){ClearItems();loadedQuery="";page=0;hasMore=false;placeholder.Text="Buscando no catálogo...";placeholder.Visible=true;}
  SetMessage(append?"Carregando mais resultados...":"Buscando \""+text+"\" no catálogo...",false);
  bool failed=false;
  try{
   var found=await catalog.SearchAsync(text,append?page+1:1,requestToken,seconds=>{
    if(!IsDisposed&&request==cancellation)SetMessage("O catálogo pediu uma pausa. Nova tentativa em "+seconds+" s...",true);
   });
   if(IsDisposed||request!=cancellation||text!=search.Text.Trim())return;
   results.BeginUpdate();
   try{
    foreach(var r in found.Results){if(r.Anime.CatalogId>0&&items.Any(i=>i.Anime.CatalogId==r.Anime.CatalogId))continue;items.Add(r);results.Items.Add(r);}
   }finally{results.EndUpdate();}
   loadedQuery=text;page=found.Page;hasMore=found.HasNextPage;
   placeholder.Visible=items.Count==0;placeholder.Text="Nenhum resultado para \""+text+"\".";
   SetMessage(items.Count==0?"Nenhum anime encontrado. Tente outro nome ou use o cadastro manual.":items.Count+" resultados. Clique duas vezes ou use Selecionar anime.",false);
   foreach(var r in items.Where(r=>r.Image==null)){var ignored=LoadThumb(r,requestToken);}
  }
  catch(OperationCanceledException){failed=true;if(!IsDisposed&&request==cancellation){lastQuery="";SetMessage("A busca demorou demais. Tente novamente ou use o cadastro manual.",true);}}
  catch(CatalogException ex){failed=true;if(!IsDisposed&&request==cancellation){lastQuery="";SetMessage(ex.Message,true);}}
  catch(Exception){failed=true;if(!IsDisposed&&request==cancellation){lastQuery="";SetMessage("Não foi possível ler a resposta do catálogo. Tente novamente ou use o cadastro manual.",true);}}
  finally{
   if(!IsDisposed&&request==cancellation){busy=false;searchButton.Enabled=true;results.Enabled=loadedQuery==search.Text.Trim();selectButton.Enabled=results.Enabled&&results.SelectedIndex>=0;moreButton.Enabled=results.Enabled&&hasMore;if(failed&&items.Count==0){placeholder.Visible=true;placeholder.Text="Use Buscar para tentar novamente ou escolha Cadastro manual.";}}
   // CancelRequest encerra e libera a fonte na próxima busca ou ao fechar a janela.
  }
 }
 async Task LoadThumb(SearchResult result,CancellationToken token){
  Image image;try{image=await Catalog.LoadImage(result.Thumb.Length>0?result.Thumb:result.Anime.Cover,token);}catch(OperationCanceledException){return;}
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
  var card=new Rectangle(e.Bounds.X+Theme.S(this,2),e.Bounds.Y+Theme.S(this,3),e.Bounds.Width-Theme.S(this,4)-1,e.Bounds.Height-Theme.S(this,6)-1);
  if(selected||e.Index==hover)using(var p=Theme.Round(card,Theme.S(this,10))){
   using(var b=new SolidBrush(selected?Theme.Selected:Theme.Row))g.FillPath(b,p);
   if(selected)using(var pen=new Pen(Color.FromArgb(150,Theme.Accent)))g.DrawPath(pen,p);
  }
  // Ícone (capa) do anime à esquerda de cada resultado.
  int th=card.Height-Theme.S(this,14),tw=(int)(th*0.72f);var thumb=new Rectangle(card.X+Theme.S(this,9),card.Y+Theme.S(this,7),tw,th);
  Theme.DrawCover(this,g,thumb,r.Image,Theme.Initial(r.Anime.Title),Theme.S(this,6));
  int x=thumb.Right+Theme.S(this,14),right=card.Right-Theme.S(this,14);
  if(r.Anime.CatalogId>0&&owned.Contains(r.Anime.CatalogId)){Rectangle chip;Theme.Chip(this,g,"NA BIBLIOTECA",Theme.Success,new Rectangle(x,card.Y+Theme.S(this,12),right-x,Theme.S(this,22)),true,out chip);right=chip.X-Theme.S(this,10);}
  bool alt=r.Alternative.Length>0;int top=card.Y+(alt?Theme.S(this,9):Theme.S(this,15));
  TextRenderer.DrawText(g,r.Anime.Title,Theme.Font(this,11.5f,FontStyle.Bold),new Rectangle(x,top,Math.Max(10,right-x),Theme.S(this,24)),Theme.Text,Theme.Line);
  if(alt)TextRenderer.DrawText(g,r.Alternative,Theme.Font(this,9.5f),new Rectangle(x,top+Theme.S(this,24),Math.Max(10,card.Right-Theme.S(this,14)-x),Theme.S(this,20)),Theme.Soft,Theme.Line);
  string episodes=r.Anime.Total==1?"1 episódio":r.Anime.Total>1?r.Anime.Total+" episódios":"Episódios não informados";
  string meta=String.Join("  ·  ",new[]{r.Format,r.Anime.Year>0?r.Anime.Year.ToString():"",episodes}.Where(s=>s.Length>0).ToArray());
  TextRenderer.DrawText(g,meta,Theme.Font(this,9),new Rectangle(x,top+(alt?Theme.S(this,45):Theme.S(this,28)),Math.Max(10,card.Right-Theme.S(this,14)-x),Theme.S(this,20)),Theme.Muted,Theme.Line);
 }
}
}
