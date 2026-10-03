using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Reflection;
using AniLista;
class InteractionTests {
 static IEnumerable<Control> Walk(Control root) { foreach(Control c in root.Controls) { yield return c; foreach(Control child in Walk(c)) yield return child; } }
 static void Assert(bool success,string name) { if(!success)throw new Exception(name); }
 static Button Button(Form form,string title) {return Walk(form).OfType<Button>().First(b=>b.Text==title||b.Text.StartsWith(title+"   "));}
 [STAThread] static int Main(){
  WebViewDependencies.Register();
  DpiForm.EnablePerMonitor();
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string folder=Path.Combine(Path.GetTempPath(),"AniLista-interactions-"+Guid.NewGuid().ToString("N"));
  var store=new LibraryStore(folder);
  var watching=new Anime{Title="Teste episódio",Status="watching",Episode=1,Total=2};
  var planned=new Anime{Title="Teste planejado",Status="planned",Episode=0,Total=12};
  var completed=new Anime{Title="Teste concluído",Status="completed",Episode=12,Total=12};
  store.Save(new List<Anime>{watching,planned,completed});
  try{
   using(var main=new MainForm(store,store.Load())){
    main.ShowInTaskbar=false;main.Opacity=0;main.Show();Application.DoEvents();
    var card=Walk(main).OfType<LinePanel>().First();
    Assert(card.Controls.OfType<Label>().All(l=>l.Right<=card.Width&&l.Left>=0),"Texto cabe dentro do cartão");
    Assert(main.Cursor.Handle==AppCursors.Arrow.Handle&&main.Cursor.Handle!=Cursors.Default.Handle,"Seta personalizada carregada do executável");
    Assert(Walk(main).OfType<TextBox>().Single().Cursor.Handle==AppCursors.Text.Handle&&AppCursors.Text.Handle!=Cursors.IBeam.Handle,"Campo usa cursor de texto personalizado");
    Assert(Button(main,"+  Adicionar anime").Cursor.Handle==AppCursors.Hand.Handle&&AppCursors.Hand.Handle!=Cursors.Hand.Handle,"Botão usa cursor personalizado");
    Button(main,"+1 episódio").PerformClick();Application.DoEvents();
    Assert(store.Load().Single(a=>a.Id==watching.Id).Status=="completed","Último episódio move para concluídos");
    Button(main,"Concluídos").PerformClick();Application.DoEvents();
    Assert(Walk(main).OfType<LinePanel>().Count()==2,"Dois cartões concluídos");
    Button(main,"Voltar a assistir").PerformClick();Application.DoEvents();
    Assert(store.Load().Single(a=>a.Id==watching.Id).Status=="watching"&&store.Load().Single(a=>a.Id==watching.Id).Episode==0,"Voltar a assistir reinicia progresso");
    Button(main,"Quero assistir").PerformClick();Application.DoEvents();
    Button(main,"Começar a assistir").PerformClick();Application.DoEvents();
    Assert(store.Load().Single(a=>a.Id==planned.Id).Status=="watching","Planejado move para assistindo");
    using(var backups=new BackupForm(store,delegate(List<Anime> entries){return 0;})){
     backups.ShowInTaskbar=false;backups.Opacity=0;backups.Show();Application.DoEvents();
     Assert(Walk(backups).OfType<ListBox>().Single().Items.Count>0&&Button(backups,"Recuperar animes").Enabled,"Tela de recuperação lista as cópias salvas");
     Capture(backups,"backups-100.png");backups.Close();
    }
    main.Close();
   }
   using(var editor=new EditorForm(new Anime(),true,"watching",delegate(Anime a){var list=store.Load();list.Add(a);store.Save(list);return true;})){
    editor.ShowInTaskbar=false;editor.Opacity=0;editor.Show();Application.DoEvents();
    Walk(editor).OfType<TextBox>().First().Text="Teste cadastro manual";
    var numbers=Walk(editor).OfType<Stepper>().OrderBy(n=>n.Left).ToArray();numbers[0].Value=5;numbers[1].Value=24;
    Button(editor,"Adicionar anime").PerformClick();Application.DoEvents();
    Assert(store.Load().Any(a=>a.Title=="Teste cadastro manual"&&a.Episode==5&&a.Total==24&&a.Status=="watching"),"Cadastro manual salva episódio e total");
   }
   SearchSafety();CardReuseAndDpi();AnitsuSaveBoundary();AnitsuDialogs(folder);
   Console.WriteLine("PASS: cartões, incremento, conclusão, três listas, reinício e cadastro manual.");return 0;
  }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
  finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
 }
 static void AnitsuDialogs(string folder){
  using(var window=new AnitsuWebViewForm(folder)){
   window.ShowInTaskbar=false;window.Opacity=0;window.Show();Application.DoEvents();Assert(!Walk(window).OfType<ComboBox>().Any(),"Janela integrada não oferece modos");Assert(!Walk(window).OfType<Button>().Any(),"Sem barra de botões adicional");Assert(!Walk(window).OfType<Label>().Any(),"Sem rodapé adicional");Assert(window.Icon!=null&&window.ShowIcon,"Janela tem ícone");Capture(window,"anitsu-integrado-100.png");window.ApplyScale(1.5f);Application.DoEvents();Capture(window,"anitsu-integrado-150.png");window.Close();
  }
  using(var results=new AnitsuResultsForm(new List<AnitsuCandidate>{new AnitsuCandidate{Name="Lain",Path="Anime/Lain"},new AnitsuCandidate{Name="Lain",Path="BD/Lain"}})){
   results.ShowInTaskbar=false;results.Opacity=0;results.Show();Application.DoEvents();var list=Walk(results).OfType<ListBox>().Single();Assert(!Button(results,"Abrir no Anitsu").Enabled,"Resultado ambíguo exige seleção");list.SelectedIndex=1;Assert(Button(results,"Abrir no Anitsu").Enabled,"Selecionar pasta habilita abertura");Capture(results,"anitsu-results-100.png");Button(results,"Abrir no Anitsu").PerformClick();Assert(results.Selected.Path=="BD/Lain","Seleção preserva caminho escolhido");
  }
 }
 sealed class AnitsuProvider:IAnitsuWorkspace {
  public int Requests,Opened;public string Title;public bool Disposed;
  public Task SearchAsync(Anime anime,IWin32Window owner){Requests++;Title=anime.Title;return Task.FromResult(0);}
  public Task ShowAsync(IWin32Window owner){Opened++;return Task.FromResult(0);}
  public void Dispose(){Disposed=true;}
 }
 static void AnitsuSaveBoundary(){
  string folder=Path.Combine(Path.GetTempPath(),"AniLista-save-boundary-"+Guid.NewGuid().ToString("N"));
  try{var store=new LibraryStore(folder);var data=store.Load();var provider=new AnitsuProvider();
   using(var main=new MainForm(store,data,null,provider))
   using(var dismiss=new System.Windows.Forms.Timer{Interval=20}){
    dismiss.Tick+=delegate{foreach(var notice in Application.OpenForms.OfType<Notice>().ToArray()){notice.DialogResult=DialogResult.OK;notice.Close();}};dismiss.Start();main.ShowInTaskbar=false;main.Opacity=0;main.Show();Application.DoEvents();
    var save=typeof(MainForm).GetMethod("SaveEntry",BindingFlags.NonPublic|BindingFlags.Instance);var anime=new Anime{Title="Boundary Lain",Status="watching"};
    Assert((bool)save.Invoke(main,new object[]{anime,true})&&provider.Requests==0,"Inclusão salva sem pesquisa automática");
    anime.Episode=1;anime.Title="Título atualizado";Assert((bool)save.Invoke(main,new object[]{anime,false})&&provider.Requests==0,"Edição não dispara busca");
    Assert(!(bool)save.Invoke(main,new object[]{new Anime{Title=anime.Title},true})&&provider.Requests==0,"Duplicata não dispara busca");
    byte[] before=File.ReadAllBytes(store.FilePath),backup=File.ReadAllBytes(store.BackupPath(0));
    var more=Button(main,"⋯");Assert(more.Right<=more.Parent.Width&&more.Top>=0,"Três pontinhos cabem no cartão");more.ContextMenuStrip.Items[0].PerformClick();Assert(provider.Requests==1&&provider.Title==anime.Title,"Menu pesquisa o título atual do cartão");Assert(more.ContextMenuStrip.Items.Count==1,"Somente pesquisar no Anitsu");Assert(((RoundButton)more).Ellipsis,"Ícone desenhado centralizado");
    Assert(before.SequenceEqual(File.ReadAllBytes(store.FilePath))&&backup.SequenceEqual(File.ReadAllBytes(store.BackupPath(0))),"Menu não altera biblioteca nem backup");
    var external=new LibraryStore(folder);var externalData=external.Load();externalData.Add(new Anime{Title="Alteração externa"});external.Save(externalData);
    Assert(!(bool)save.Invoke(main,new object[]{new Anime{Title="Não salvo"},true})&&provider.Requests==1,"Falha no salvamento não dispara busca");main.Close();Assert(provider.Disposed,"Fechar app encerra integração");
   }
  }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
 }
 sealed class PendingCatalog:ICatalogClient {
  public readonly List<TaskCompletionSource<SearchPage>> Pending=new List<TaskCompletionSource<SearchPage>>();
  public readonly List<int> Pages=new List<int>();
  public Task<SearchPage> SearchAsync(string text,int page,CancellationToken token,Action<int> waiting){var source=new TaskCompletionSource<SearchPage>();Pending.Add(source);Pages.Add(page);return source.Task;}
 }
 static SearchPage Page(int number,bool more,params int[] ids){return new SearchPage(ids.Select(id=>new SearchResult{Anime=new Anime{CatalogId=id,Title="Teste "+id}}).ToList(),number,more);}
 static void PumpUntil(Func<bool> done){var clock=Stopwatch.StartNew();while(!done()){Application.DoEvents();Thread.Sleep(5);if(clock.ElapsedMilliseconds>5000)throw new Exception("Tempo de espera excedido no teste de interface");}Application.DoEvents();}
 static void SearchSafety(){
  var catalog=new PendingCatalog();int selected=0;
  using(var form=new SearchForm((owner,anime)=>{selected++;return false;},null,catalog)){
   form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();
   var input=Walk(form).OfType<TextBox>().Single();var list=Walk(form).OfType<ListBox>().Single();
   input.Text="naruto";Button(form,"Buscar").PerformClick();catalog.Pending[0].SetResult(Page(1,true,1));PumpUntil(()=>list.Items.Count==1);
   list.SelectedIndex=0;Assert(Button(form,"Selecionar anime").Enabled,"Resultado atual selecionável");
   input.Text="bleach";Assert(list.Items.Count==0&&!list.Enabled&&!Button(form,"Selecionar anime").Enabled,"Digitar limpa e bloqueia resultados anteriores imediatamente");
   Button(form,"Selecionar anime").PerformClick();Assert(selected==0,"Resultado anterior não pode ser selecionado");
   Button(form,"Buscar").PerformClick();input.Text="one piece";Button(form,"Buscar").PerformClick();
   catalog.Pending[1].SetResult(Page(1,false,2));Application.DoEvents();Assert(list.Items.Count==0,"Resposta atrasada descartada");
   catalog.Pending[2].SetResult(Page(1,true,3));PumpUntil(()=>list.Items.Count==1);
   Button(form,"Carregar mais").PerformClick();Assert(catalog.Pages.Last()==2,"Carregar mais solicita página seguinte");
   catalog.Pending[3].SetResult(Page(2,false,3,4));PumpUntil(()=>list.Items.Count==2);
   Assert(!Button(form,"Carregar mais").Enabled,"Fim da paginação desabilita botão e evita duplicatas");
   list.SelectedIndex=1;Button(form,"Selecionar anime").PerformClick();Assert(selected==1,"Seleção volta a funcionar após carregar");
   form.Close();
  }
 }
 sealed class CoversHandler:HttpMessageHandler {
  public int Count;readonly byte[] bytes;
  public CoversHandler(){using(var image=new Bitmap(4,6))using(var memory=new MemoryStream()){using(var g=Graphics.FromImage(image))g.Clear(Color.MediumPurple);image.Save(memory,System.Drawing.Imaging.ImageFormat.Png);bytes=memory.ToArray();}}
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Interlocked.Increment(ref Count);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});}
 }
 static void Capture(Form form,string filename){Directory.CreateDirectory("qa");using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine("qa",filename),System.Drawing.Imaging.ImageFormat.Png);}}
 static void CardReuseAndDpi(){
  string folder=Path.Combine(Path.GetTempPath(),"AniLista-cards-"+Guid.NewGuid().ToString("N"));
  var store=new LibraryStore(folder);var handler=new CoversHandler();
  using(var http=new HttpClient(handler)){
   var covers=new CoverService(http);var entries=Enumerable.Range(1,40).Select(i=>new Anime{Title="Anime "+i.ToString("00"),Status="watching",Episode=1,Total=12,CatalogId=i,Cover="https://s4.anilist.co/test-"+i}).ToList();store.Save(entries);
   try{
    using(var main=new MainForm(store,store.Load(),covers)){
     Assert(handler.Count==0,"Capas não são baixadas ao construir cartões fora da tela");
     main.ShowInTaskbar=false;main.Opacity=0;main.Show();Application.DoEvents();PumpUntil(()=>handler.Count>0);
     Assert(handler.Count<40,"Só capas da área visível são requisitadas");
     var original=Walk(main).OfType<AnimeCard>().ToDictionary(c=>((Anime)c.Tag).Id);
     var first=original.Values.First(c=>((Anime)c.Tag).Title=="Anime 01");
     first.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().First().PerformClick();Application.DoEvents();
     var current=Walk(main).OfType<AnimeCard>().ToDictionary(c=>((Anime)c.Tag).Id);
     Assert(current.All(p=>Object.ReferenceEquals(p.Value,original[p.Key])),"Atualizar episódio reaproveita todos os cartões");
     first.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().First().PerformClick();
     Assert(store.Load().Single(a=>a.Title=="Anime 01").Episode==3,"Botão usa os dados atualizados, sem captura antiga");
     main.ApplyScale(1f);Application.DoEvents();Capture(main,"biblioteca-100.png");
     main.ApplyScale(1.5f);Application.DoEvents();Capture(main,"biblioteca-150.png");
     Assert(Math.Abs(Theme.ScaleFor(first)-1.5f)<0.01&&Theme.S(first,100)==150,"Métricas usam DPI da janela");
     foreach(var card in current.Values)Assert(card.Controls.OfType<Label>().All(l=>l.Right<=card.Width+1),"Texto permanece dentro do cartão em 150%");
     using(var other=new DpiForm())Assert(Theme.S(other,100)==100,"Outra janela mantém escala independente");
     main.ApplyScale(2f);Application.DoEvents();Capture(main,"biblioteca-200.png");
     foreach(var card in current.Values){
      Assert(card.Width<=card.Parent.ClientSize.Width,"Cartão cabe na área disponível em 200%");
      var actions=card.Controls.OfType<FlowLayoutPanel>().Single();Assert(actions.Controls.Cast<Control>().All(c=>c.Right<=actions.ClientSize.Width),"Todos os botões do cartão cabem em 200%");
     }
     var addButton=Button(main,"+  Adicionar anime");var heading=Walk(main).OfType<Label>().Single(l=>l.Text=="Assistindo");
     Assert(!heading.Bounds.IntersectsWith(addButton.Bounds),"Título e botão não se sobrepõem em tela pequena com 200%");
     main.ApplyScale(1f);Application.DoEvents();Assert(Theme.S(first,100)==100,"Escala retorna a 100%");main.Close();
    }
   }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
  }
 }

}
