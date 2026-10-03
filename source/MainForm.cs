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
public sealed class MainForm:DpiForm {
 static readonly string[] StatusKeys={"watching","planned","completed"};
 readonly CoverService covers;
 readonly IAnitsuWorkspace anitsuWorkspace;
 Panel anitsuArea;RoundButton returnLibrary;
 readonly LibraryStore store;List<Anime> entries;string status="watching";
 readonly RoundButton[] navigation=new RoundButton[3];Label heading,summary,feedback;TextBox filter;FlowLayoutPanel cards;Panel main,sidebar;Label section,smallLogo;Brand logo;System.Windows.Forms.Timer filterTimer,coverTimer;
 readonly Dictionary<string,AnimeCard> cardIndex=new Dictionary<string,AnimeCard>(StringComparer.Ordinal);
 public MainForm(LibraryStore library,List<Anime> data,CoverService coverService=null,IAnitsuWorkspace integration=null){
  covers=coverService??CoverService.Shared;
  store=library;entries=data??new List<Anime>();covers.Folder=Path.Combine(store.Folder,"capas");
  anitsuWorkspace=integration??new AnitsuWorkspace(store.Folder);
  Text="AniLista — Minha biblioteca de animes";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font(this,10);Theme.DarkTitle(this);
  StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.None;MinimumSize=Theme.S(this,920,600);
  Rectangle work=Screen.PrimaryScreen.WorkingArea;Size=new Size(Math.Min(Theme.S(this,1220),work.Width-40),Math.Min(Theme.S(this,800),work.Height-40));
  if(Theme.AppIcon!=null)Icon=Theme.AppIcon;
  BuildLayout();Render(false);
  LayoutScaleChanged+=delegate{Rectangle area=Screen.FromHandle(Handle).WorkingArea;MinimumSize=new Size(Math.Min(Theme.S(this,920),area.Width-20),Math.Min(Theme.S(this,600),area.Height-20));AdaptLayout();ResizeCards();ScheduleCovers();};
  if(store.Recovered)Shown+=delegate{Notice.Tell(this,"Biblioteca recuperada","A cópia de segurança da biblioteca foi recuperada. O arquivo anterior foi preservado.");};
  KeyPreview=true;KeyDown+=delegate(object sender,KeyEventArgs e){
   if(anitsuArea!=null&&anitsuArea.Visible)return;
   if(e.Control&&e.KeyCode==Keys.N){e.Handled=true;e.SuppressKeyPress=true;AddAnime();}
   else if(e.Control&&e.KeyCode==Keys.F){e.Handled=true;e.SuppressKeyPress=true;filter.Focus();filter.SelectAll();}
  };
  FormClosed+=delegate{anitsuWorkspace.Dispose();filterTimer.Dispose();coverTimer.Dispose();DisposeCards();};
 }
 void BuildLayout(){
  sidebar=new Panel{Dock=DockStyle.Left,Width=Theme.S(this,236),BackColor=Theme.Sidebar};Controls.Add(sidebar);
  sidebar.Paint+=delegate(object s,PaintEventArgs e){using(var p=new Pen(Theme.Border))e.Graphics.DrawLine(p,sidebar.Width-1,0,sidebar.Width-1,sidebar.Height);};
  logo=new Brand();Theme.Place(this,logo,24,30,196,52);sidebar.Controls.Add(logo);
  smallLogo=Theme.Label(this,"AniLista",12,Theme.Accent,FontStyle.Bold);Theme.Place(this,smallLogo,6,30,88,52);smallLogo.TextAlign=ContentAlignment.MiddleCenter;smallLogo.Visible=false;sidebar.Controls.Add(smallLogo);
  section=Theme.Label(this,"MINHA BIBLIOTECA",8.5f,Theme.Muted,FontStyle.Bold);Theme.Place(this,section,26,112,190,20);sidebar.Controls.Add(section);
  for(int i=0;i<StatusKeys.Length;i++){
   string selected=StatusKeys[i];var button=new RoundButton{Nav=true,Under=Theme.Sidebar,Dot=Theme.StatusColor(selected),Text=Theme.StatusName(selected),Font=Theme.Font(this,10,FontStyle.Bold),Cursor=Cursors.Hand,BackColor=Theme.Sidebar,ForeColor=Theme.Muted};
   Theme.Place(this,button,18,142+i*54,200,46);button.Click+=delegate{ShowLibrary();status=selected;filter.Clear();Render(false);};navigation[i]=button;sidebar.Controls.Add(button);
  }
  main=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Background,Padding=new Padding(Theme.S(this,30),Theme.S(this,22),Theme.S(this,24),Theme.S(this,8))};Controls.Add(main);main.BringToFront();
  var header=new Panel{Dock=DockStyle.Top,Height=Theme.S(this,96)};
  heading=Theme.Label(this,"Assistindo",26,Theme.Text,FontStyle.Bold);Theme.Place(this,heading,0,0,520,50);header.Controls.Add(heading);
  summary=Theme.Label(this,"",11,Theme.Muted);Theme.Place(this,summary,2,52,520,26);header.Controls.Add(summary);
  var add=Theme.Button(this,"+  Adicionar anime",true);add.Size=Theme.S(this,186,44);add.Click+=delegate{AddAnime();};header.Controls.Add(add);
  header.Resize+=delegate{
   bool stack=header.Width<Theme.S(this,520);int height=Theme.S(this,stack?150:96);if(header.Height!=height)header.Height=height;
   add.Location=new Point(header.Width-add.Width,Theme.S(this,stack?90:6));heading.Width=summary.Width=Math.Max(1,stack?header.Width:header.Width-add.Width-Theme.S(this,16));
  };
  var searchRow=new Panel{Dock=DockStyle.Top,Height=Theme.S(this,60)};
  filter=Theme.TextBox(this);filter.MaxLength=100;Theme.Cue(filter,"Filtrar por nome  (Ctrl+F)");searchRow.Controls.Add(Theme.Wrap(this,filter,0,0,360,40,AnchorStyles.Top|AnchorStyles.Left,true));
  filter.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Escape&&filter.TextLength>0){filter.Clear();e.Handled=true;e.SuppressKeyPress=true;}};
  filterTimer=new System.Windows.Forms.Timer{Interval=170};filterTimer.Tick+=delegate{filterTimer.Stop();RenderCards(false);};filter.TextChanged+=delegate{filterTimer.Stop();filterTimer.Start();};
  var footer=new Panel{Dock=DockStyle.Bottom,Height=Theme.S(this,26)};feedback=Theme.Label(this,store.BackupWarning??"Salvo automaticamente neste computador",9,Theme.Muted);feedback.Dock=DockStyle.Fill;feedback.TextAlign=ContentAlignment.MiddleLeft;footer.Controls.Add(feedback);
  var backups=Theme.Button(this,"Backup e recuperação");backups.Dock=DockStyle.Right;backups.Width=Theme.S(this,182);backups.Font=Theme.Font(this,9);backups.Click+=delegate{OpenBackups();};footer.Controls.Add(backups);
  cards=new FlowPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true,BackColor=Theme.Background,Padding=new Padding(0),FlowDirection=FlowDirection.LeftToRight};cards.Resize+=delegate{ResizeCards();ScheduleCovers();};cards.Scroll+=delegate{ScheduleCovers();};
  coverTimer=new System.Windows.Forms.Timer{Interval=80};coverTimer.Tick+=delegate{coverTimer.Stop();LoadVisibleCovers();};
  Shown+=delegate{ScheduleCovers();};
  main.Controls.Add(cards);main.Controls.Add(footer);main.Controls.Add(searchRow);main.Controls.Add(header);
  Resize+=delegate{AdaptLayout();};AdaptLayout();
 }
 internal void ShowAnitsu(AnitsuWebViewForm browser){
  if(anitsuArea==null){
   anitsuArea=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Background};Controls.Add(anitsuArea);
   returnLibrary=Theme.Button(this,"← Biblioteca");returnLibrary.Dock=DockStyle.Bottom;returnLibrary.Height=Theme.S(this,44);returnLibrary.Under=Theme.Sidebar;returnLibrary.Click+=delegate{ShowLibrary();};sidebar.Controls.Add(returnLibrary);
  }
  if(browser.Parent!=anitsuArea){browser.TopLevel=false;browser.FormBorderStyle=FormBorderStyle.None;browser.ShowInTaskbar=false;browser.MinimumSize=Size.Empty;browser.Dock=DockStyle.Fill;anitsuArea.Controls.Add(browser);}
  main.Visible=false;anitsuArea.Visible=true;anitsuArea.BringToFront();returnLibrary.Visible=true;browser.Show();browser.BringToFront();browser.Focus();
 }
 void ShowLibrary(){
  var workspace=anitsuWorkspace as AnitsuWorkspace;if(workspace!=null)workspace.PauseSearch();
  if(anitsuArea!=null)anitsuArea.Visible=false;if(returnLibrary!=null)returnLibrary.Visible=false;main.Visible=true;main.BringToFront();ScheduleCovers();
 }
 void AdaptLayout(){
  if(sidebar==null||cards==null)return;
  bool compact=ClientSize.Width<Theme.S(this,820);
  sidebar.Width=Theme.S(this,compact?100:236);logo.Visible=!compact;smallLogo.Visible=compact;section.Visible=!compact;
  for(int i=0;i<navigation.Length;i++){
   var button=navigation[i];button.Nav=!compact;button.Dot=compact?Color.Transparent:Theme.StatusColor(StatusKeys[i]);
   button.Left=Theme.S(this,compact?6:18);button.Width=Theme.S(this,compact?88:200);
   if(compact)button.Text=(i==1?"Quero ver":Theme.StatusName(StatusKeys[i]));else button.Text=Theme.StatusName(StatusKeys[i])+"   "+entries.Count(a=>a.Status==StatusKeys[i]);
  }
  if(filter!=null&&filter.Parent!=null)filter.Parent.Width=Math.Max(1,Math.Min(Theme.S(this,360),main.ClientSize.Width-main.Padding.Horizontal));
  ResizeCards();
 }
 void DisposeCards(){foreach(Control control in cards.Controls.Cast<Control>().ToArray())control.Dispose();cards.Controls.Clear();cardIndex.Clear();}
 void Render(bool keepScroll){
  heading.Text=Theme.StatusName(status);
  for(int i=0;i<StatusKeys.Length;i++){
   string key=StatusKeys[i];int count=entries.Count(a=>a.Status==key);navigation[i].Text=Theme.StatusName(key)+"   "+count;
   navigation[i].BackColor=key==status?Theme.Selected:Theme.Sidebar;navigation[i].ForeColor=key==status?Theme.SelectedText:Theme.Muted;navigation[i].Invalidate();
  }
  int selectedCount=entries.Count(a=>a.Status==status);summary.Text=selectedCount==1?"1 anime nesta lista":selectedCount+" animes nesta lista";
  AdaptLayout();RenderCards(keepScroll);
 }
 void RenderCards(bool keepScroll){
  int scroll=keepScroll?-cards.AutoScrollPosition.Y:0;
  string text=filter.Text.Trim();
  var visible=entries.Where(a=>a.Status==status&&(text.Length==0||a.Title.IndexOf(text,StringComparison.CurrentCultureIgnoreCase)>=0)).OrderByDescending(a=>a.Updated,StringComparer.Ordinal).ThenBy(a=>a.Title).ToList();
  var ids=new HashSet<string>(visible.Select(a=>a.Id),StringComparer.Ordinal);
  cards.SuspendLayout();
  try{
   foreach(string id in cardIndex.Keys.Where(id=>!ids.Contains(id)).ToArray()){var old=cardIndex[id];cards.Controls.Remove(old);old.Dispose();cardIndex.Remove(id);}
   foreach(Control empty in cards.Controls.Cast<Control>().Where(c=>c.Tag is string).ToArray()){cards.Controls.Remove(empty);empty.Dispose();}
   if(visible.Count==0)cards.Controls.Add(CreateEmpty(text.Length>0));
   else for(int i=0;i<visible.Count;i++){
    Anime anime=visible[i];AnimeCard card;
    if(!cardIndex.TryGetValue(anime.Id,out card)){
     card=new AnimeCard(anime,changed=>{changed.Validate();SaveEntry(changed,false);},a=>Edit(a),a=>{
      if(Notice.Ask(this,"Remover anime","\""+a.Title+"\" será removido da sua biblioteca.","Remover","Cancelar",true))SaveList(entries.Where(e=>e.Id!=a.Id).Select(e=>e.Copy()).ToList());
     },Theme.ScaleFor(this),covers,a=>SearchInAnitsu(a));cardIndex.Add(anime.Id,card);cards.Controls.Add(card);
    }else card.Bind(anime);
    cards.Controls.SetChildIndex(card,i);
   }
   ResizeCards();
  }finally{cards.ResumeLayout(true);}
  cards.AutoScrollPosition=new Point(0,scroll);ScheduleCovers();
 }
 void ScheduleCovers(){if(coverTimer==null||IsDisposed)return;coverTimer.Stop();coverTimer.Start();}
 void LoadVisibleCovers(){
  var viewport=new Rectangle(0,0,cards.ClientSize.Width,cards.ClientSize.Height);
  foreach(var card in cardIndex.Values)card.SetCoverVisible(card.Bounds.IntersectsWith(viewport));
 }
 Panel CreateEmpty(bool filtered){
  var empty=new LinePanel{Width=Theme.S(this,760),Height=Theme.S(this,220),Margin=new Padding(0,Theme.S(this,4),0,Theme.S(this,14)),Tag="empty"};
  var title=Theme.Label(this,filtered?"Nenhum anime com esse nome":"Sua lista está pronta para começar",17,Theme.Text,FontStyle.Bold);title.BackColor=Theme.Surface;title.SetBounds(Theme.S(this,28),Theme.S(this,32),empty.Width-Theme.S(this,56),Theme.S(this,36));title.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;empty.Controls.Add(title);
  string hint=status=="watching"?"Adicione o que está assistindo e registre seu episódio atual.":status=="planned"?"Guarde aqui os animes que você quer assistir depois.":"Reúna os animes que você já terminou.";
  var detail=Theme.Label(this,filtered?"Tente outro nome ou limpe o filtro (Esc).":hint,11,Theme.Muted);detail.BackColor=Theme.Surface;detail.SetBounds(Theme.S(this,28),Theme.S(this,78),empty.Width-Theme.S(this,56),Theme.S(this,48));detail.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;empty.Controls.Add(detail);
  var button=Theme.Button(this,filtered?"Limpar filtro":"+  Adicionar anime",!filtered);button.Under=Theme.Surface;button.SetBounds(Theme.S(this,28),Theme.S(this,146),Theme.S(this,186),Theme.S(this,42));
  button.Click+=delegate{if(filtered)filter.Clear();else AddAnime();};empty.Controls.Add(button);
  return empty;
 }
 void ResizeCards(){
  int available=Math.Max(1,cards.ClientSize.Width-(cards.VerticalScroll.Visible?0:SystemInformation.VerticalScrollBarWidth)-Theme.S(this,2));
  int gap=Theme.S(this,14),columns=Math.Max(1,Math.Min(3,available/(Theme.S(this,400)+gap)));
  foreach(Control card in cards.Controls)card.Width=card.Tag is string?available:available/columns-gap;
 }
 IWin32Window TopWindow(){Form top=Application.OpenForms.Cast<Form>().LastOrDefault(f=>f.Visible&&!(f is Notice));return top??this;}
 bool SaveList(List<Anime> next){
  try{store.Save(next);entries=next;feedback.Text=store.BackupWarning??"Salvo às "+DateTime.Now.ToString("HH:mm");Render(true);if(store.BackupWarning!=null)Notice.Tell(TopWindow(),"Biblioteca salva, backup pendente",store.BackupWarning);return true;}
  catch(Exception ex){Notice.Tell(TopWindow(),"Não foi possível salvar","As alterações anteriores continuam preservadas.\n\n"+ex.Message);return false;}
 }
 void OpenBackups(){try{using(var dialog=new BackupForm(store,backup=>{
  var merged=LibraryStore.MergeMissing(entries,backup);int added=merged.Count-entries.Count;if(added==0)return 0;return SaveList(merged)?added:-1;
 }))dialog.ShowDialog(this);}catch(Exception ex){Notice.Tell(this,"Não foi possível abrir as cópias",ex.Message);}}
 bool SaveEntry(Anime entry,bool isNew){
  if(isNew&&entries.Any(a=>(entry.CatalogId>0&&a.CatalogId==entry.CatalogId)||String.Equals(a.Title.Trim(),entry.Title.Trim(),StringComparison.CurrentCultureIgnoreCase))){
   Notice.Tell(TopWindow(),"Anime já adicionado","Esse anime já está na sua biblioteca. Use Editar para mudar a lista ou o episódio.");return false;
  }
  var next=entries.Select(a=>a.Copy()).ToList();if(isNew)next.Add(entry);else{int index=next.FindIndex(a=>a.Id==entry.Id);if(index<0)return false;next[index]=entry;}return SaveList(next);
 }
 async void SearchInAnitsu(Anime anime){try{await anitsuWorkspace.SearchAsync(anime,this);}catch(Exception e){if(!IsDisposed)Notice.Tell(this,"Anitsu",e.Message);}}
 void Edit(Anime entry){using(var editor=new EditorForm(entry,false,entry.Status,a=>SaveEntry(a,false)))editor.ShowDialog(this);}
 void AddAnime(){
  if(Application.OpenForms.OfType<SearchForm>().Any())return;
  var owned=new HashSet<int>(entries.Where(a=>a.CatalogId>0).Select(a=>a.CatalogId));
  using(var dialog=new SearchForm(delegate(SearchForm owner,Anime selected){
   using(var editor=new EditorForm(selected,true,status,a=>SaveEntry(a,true)))return editor.ShowDialog(owner)==DialogResult.OK;
  },owned))dialog.ShowDialog(this);
 }
}
}
