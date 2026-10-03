using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AniLista;

class CoreTests {
 sealed class Handler:HttpMessageHandler {
  public int Count;public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Reply;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Interlocked.Increment(ref Count);return Reply(request,token);}
 }
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static HttpResponseMessage Page(int id,bool more){return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"data\":{\"Page\":{\"pageInfo\":{\"hasNextPage\":"+(more?"true":"false")+"},\"media\":[{\"id\":"+id+",\"title\":{\"romaji\":\"Teste 日本語\",\"english\":\"Test\"},\"episodes\":12,\"coverImage\":{\"large\":\"\",\"medium\":\"\"}}]}}}")};}
 static int Main(){
  string folder=Path.Combine(Path.GetTempPath(),"AniLista-core-"+Guid.NewGuid().ToString("N"));
  try{Backups(folder);StorageSafety(Path.Combine(folder,"seguranca"));CatalogTests().GetAwaiter().GetResult();CoverTests().GetAwaiter().GetResult();Console.WriteLine("PASS: backup único, migração, recuperação, conflitos de gravação, exportação, validação, paginação, cache e capas.");return 0;}
  catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
  finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
 }
 static void Backups(string folder){
  var store=new LibraryStore(folder);var anime=new Anime{Title="Teste 日本語",Status="watching",Total=50};
  for(int episode=0;episode<10;episode++){anime.Episode=episode;anime.Validate();store.Save(new List<Anime>{anime.Copy()});}
  Assert(Directory.GetFiles(folder,"biblioteca.json.bak*").Length==1,"Só um arquivo de backup após dez salvamentos");
  Assert(File.ReadAllBytes(store.FilePath).SequenceEqual(File.ReadAllBytes(store.BackupPath(0))),"Backup acompanha a última gravação completa");
  var restored=new LibraryStore(folder);
  File.WriteAllText(store.FilePath,"corrompido");
  var recovered=restored.Load();Assert(restored.Recovered&&recovered[0].Episode==9,"Backup único recupera a última gravação válida");
  Assert(File.ReadAllText(store.FilePath).Contains("Teste"),"Arquivo principal restaurado imediatamente");
  recovered[0].Episode=10;restored.Save(recovered);
  File.WriteAllText(store.FilePath,"outra corrupção");
  Assert(new LibraryStore(folder).Load()[0].Episode==10,"Salvar depois da recuperação preserva a última versão");
  Assert(Directory.GetFiles(folder,"biblioteca.json.bak*").Length==1,"Recuperações não criam backups adicionais");
  File.Delete(store.FilePath);Assert(new LibraryStore(folder).Load()[0].Episode==10,"Principal ausente recupera o backup único");
  store.Load();
  var bad=recovered[0].Copy();bad.Status="inválido";bool rejected=false;
  try{store.Save(new List<Anime>{bad});}catch{rejected=true;}Assert(rejected,"Dados inválidos rejeitados antes de substituir biblioteca");
  Assert(new LibraryStore(folder).Load()[0].Episode==10,"Falha de validação preserva biblioteca atual");
  File.Copy(store.FilePath,store.FilePath+".bak.4");File.WriteAllText(store.FilePath,"inválido");File.WriteAllText(store.BackupPath(0),"inválido");
  Assert(new LibraryStore(folder).Load()[0].Episode==10&&Directory.GetFiles(folder,"biblioteca.json.bak*").Length==1,"Migração recupera backup antigo antes de remover os numerados");
  File.WriteAllText(store.BackupPath(0),"inválido");new LibraryStore(folder).Load();Assert(new LibraryStore(folder).ReadBackup(store.BackupPath(0))[0].Episode==10,"Principal válido repara backup corrompido");
  File.WriteAllText(store.FilePath,"inválido");File.WriteAllText(store.BackupPath(0),"também inválido");rejected=false;try{new LibraryStore(folder).Load();}catch(IOException){rejected=true;}Assert(rejected&&File.ReadAllText(store.FilePath)=="inválido","Nenhum arquivo válido não resulta em biblioteca vazia silenciosa");
 }
 static void StorageSafety(string folder){
  var store=new LibraryStore(folder);
  var first=new Anime{Title="Anime atual",Status="watching",Episode=1,Total=100,CatalogId=1};
  var lost=new Anime{Title="Anime para recuperar",Status="completed",Episode=12,Total=12,CatalogId=2};
  store.Save(new List<Anime>{first.Copy(),lost.Copy()});
  string manual=Path.Combine(folder,"copia-manual.json");store.Export(manual);
  for(int i=2;i<=15;i++){first.Episode=i;store.Save(new List<Anime>{first.Copy()});}
  Assert(store.GetBackups().Count==1,"Lista de recuperação mostra somente o backup automático único");
  var merged=LibraryStore.MergeMissing(store.Load(),store.ReadBackup(manual));
  Assert(merged.Count==2&&merged.Single(a=>a.CatalogId==1).Episode==15,"Recupera exportação manual sem regredir episódio atual");
  var renamed=first.Copy();renamed.Id=Guid.NewGuid().ToString("N");renamed.Title="Nome alternativo";
  Assert(LibraryStore.MergeMissing(merged,new List<Anime>{renamed}).Count==2,"Recuperação não duplica animes do mesmo catálogo");
  store.Save(merged);
  string exported=Path.Combine(folder,"exportada.json");store.Export(exported);Assert(store.ReadBackup(exported).Count==2,"Exportação contém a biblioteca completa");
  bool exportRejected=false;try{store.Export(store.BackupPath(0));}catch(IOException){exportRejected=true;}Assert(exportRejected&&store.ReadBackup(store.BackupPath(0)).Count==2,"Exportação não sobrescreve backup automático");
  var stale=new LibraryStore(folder);var outdated=stale.Load();var latest=store.Load();latest[0].Episode=20;store.Save(latest);
  bool rejected=false;try{stale.Save(outdated);}catch(IOException){rejected=true;}
  Assert(rejected&&new LibraryStore(folder).Load()[0].Episode==20,"Instância antiga não sobrescreve biblioteca mais recente");
  var unloaded=new LibraryStore(folder);rejected=false;try{unloaded.Save(new List<Anime>());}catch(IOException){rejected=true;}Assert(rejected,"Biblioteca existente exige leitura antes de salvar");
  latest=store.Load();File.Delete(store.FilePath);rejected=false;try{store.Save(latest);}catch(IOException){rejected=true;}Assert(rejected,"Arquivo removido com app aberto não é sobrescrito silenciosamente");
  rejected=false;try{store.Export(exported);}catch(IOException){rejected=true;}Assert(rejected,"Exportação não transforma biblioteca desaparecida em cópia vazia");
  var recovered=new LibraryStore(folder);Assert(recovered.Load().Count==2&&recovered.Recovered,"Recupera backup único sem arquivo principal");
  var validBytes=File.ReadAllBytes(store.FilePath);File.WriteAllText(exported,"inválido");rejected=false;try{store.ReadBackup(exported);}catch{rejected=true;}Assert(rejected&&validBytes.SequenceEqual(File.ReadAllBytes(store.FilePath)),"Importação inválida preserva biblioteca");
  var blocked=new LibraryStore(Path.Combine(folder,"falha"));Directory.CreateDirectory(blocked.BackupPath(0));
  rejected=false;try{blocked.Save(new List<Anime>{first});}catch(IOException){rejected=true;}Assert(rejected&&!File.Exists(blocked.FilePath),"Falha ao criar backup impede gravação desprotegida");
  var fresh=new LibraryStore(Path.Combine(folder,"nova"));fresh.Load();fresh.Export(Path.Combine(folder,"vazia.json"));Assert(fresh.ReadBackup(Path.Combine(folder,"vazia.json")).Count==0,"Uma biblioteca nova pode ser exportada vazia");
  Assert(Path.IsPathRooted(LibraryStore.DefaultFolder)&&LibraryStore.DefaultFolder==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AniLista"),"Pasta de dados fixa no AppData, independente da localização do executável");
  var transaction=new LibraryStore(Path.Combine(folder,"transaction"));transaction.Load();
  var before=new Anime{Title="Gravação bloqueada",Status="watching",Episode=1,Total=12};transaction.Save(new List<Anime>{before});
  byte[] original=File.ReadAllBytes(transaction.FilePath),originalBackup=File.ReadAllBytes(transaction.BackupPath(0));
  using(var held=new FileStream(transaction.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){
   var change=before.Copy();change.Episode=2;rejected=false;try{transaction.Save(new List<Anime>{change});}catch(IOException){rejected=true;}
   Assert(rejected,"Gravação com principal bloqueado é rejeitada");
  }
  Assert(original.SequenceEqual(File.ReadAllBytes(transaction.FilePath))&&originalBackup.SequenceEqual(File.ReadAllBytes(transaction.BackupPath(0))),"Falha ao trocar principal preserva também o backup anterior");
  using(var held=new FileStream(transaction.BackupPath(0),FileMode.Open,FileAccess.Read,FileShare.Read)){
   var change=before.Copy();change.Episode=2;rejected=false;try{transaction.Save(new List<Anime>{change});}catch(IOException){rejected=true;}
   Assert(rejected,"Backup bloqueado impede troca desprotegida do principal");
  }
  Assert(original.SequenceEqual(File.ReadAllBytes(transaction.FilePath))&&originalBackup.SequenceEqual(File.ReadAllBytes(transaction.BackupPath(0))),"Backup bloqueado preserva os dois arquivos");
  Assert(!Directory.GetFiles(transaction.Folder,"*.tmp").Any()&&Directory.GetFiles(transaction.Folder,"*.bak*").Length==1,"Falhas não deixam temporários nem backups adicionais");
  var next=before.Copy();next.Episode=2;transaction.Save(new List<Anime>{next});
  Assert(new LibraryStore(transaction.Folder).Load()[0].Episode==2&&File.ReadAllBytes(transaction.FilePath).SequenceEqual(File.ReadAllBytes(transaction.BackupPath(0))),"Nova tentativa salva e sincroniza a cópia única");
  File.WriteAllBytes(transaction.BackupPath(0),originalBackup);new LibraryStore(transaction.Folder).Load();
  Assert(File.ReadAllBytes(transaction.FilePath).SequenceEqual(File.ReadAllBytes(transaction.BackupPath(0))),"Reabrir sincroniza backup válido que ficou antigo após interrupção");
 }
 static async Task CatalogTests(){
  var payloads=new List<string>();var handler=new Handler();
  handler.Reply=async (request,token)=>{string body=await request.Content.ReadAsStringAsync();payloads.Add(body);return Page(handler.Count,handler.Count==1);};
  using(var http=new HttpClient(handler)){
   var client=new CatalogClient(http,(time,token)=>Task.FromResult(0));
   var one=await client.SearchAsync("naruto",1,CancellationToken.None,null);one.Results[0].Anime.Title="modificado";
   var cached=await client.SearchAsync("NARUTO",1,CancellationToken.None,null);
   Assert(handler.Count==1&&cached.Results[0].Anime.Title=="Teste 日本語"&&cached.HasNextPage,"Cache retorna cópias independentes");
   var two=await client.SearchAsync("naruto",2,CancellationToken.None,null);
   Assert(handler.Count==2&&two.Page==2&&!two.HasNextPage,"Página seguinte tem metadados próprios");
   Assert(payloads[1].Contains("\"page\":2")&&payloads[1].Contains("hasNextPage"),"Consulta envia página e solicita hasNextPage");
  }
  var retry=new Handler();retry.Reply=(request,token)=>{
   if(retry.Count==1){var r=new HttpResponseMessage((HttpStatusCode)429);r.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2));return Task.FromResult(r);}
   return Task.FromResult(Page(3,false));
  };
  using(var http=new HttpClient(retry)){
   var waits=new List<int>();double waited=0;
   var client=new CatalogClient(http,(time,token)=>{waited+=time.TotalSeconds;return Task.FromResult(0);});
   await client.SearchAsync("bleach",1,CancellationToken.None,n=>waits.Add(n));
   Assert(retry.Count==2&&waits.SequenceEqual(new[]{2,1})&&waited>=2,"Retry-After respeitado com contagem regressiva");
  }
  var limited=new Handler();limited.Reply=(request,token)=>{var r=new HttpResponseMessage((HttpStatusCode)429);r.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));return Task.FromResult(r);};
  using(var http=new HttpClient(limited))using(var cancellation=new CancellationTokenSource()){
   var started=new TaskCompletionSource<bool>();
   var client=new CatalogClient(http,async (time,token)=>{started.TrySetResult(true);await Task.Delay(Timeout.Infinite,token);});
   var pending=client.SearchAsync("one piece",1,cancellation.Token,null);await started.Task;cancellation.Cancel();bool cancelled=false;
   try{await pending;}catch(OperationCanceledException){cancelled=true;}
   Assert(cancelled&&limited.Count==1,"Espera de limite pode ser cancelada sem repetir consulta");
  }
 }
 static async Task CoverTests(){
  byte[] png;using(var image=new Bitmap(2,2))using(var stream=new MemoryStream()){image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);png=stream.ToArray();}
  int active=0,maximum=0;var handler=new Handler();
  handler.Reply=async (request,token)=>{
   int current=Interlocked.Increment(ref active);int old;
   do{old=maximum;if(old>=current)break;}while(Interlocked.CompareExchange(ref maximum,current,old)!=old);
   try{await Task.Delay(20,token);return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(png)};}finally{Interlocked.Decrement(ref active);}
  };
  using(var http=new HttpClient(handler)){
   var service=new CoverService(http);
   var images=await Task.WhenAll(Enumerable.Range(0,12).Select(i=>service.LoadImageAsync("https://s4.anilist.co/test-"+i,CancellationToken.None)));
   Assert(maximum<=4&&images.All(i=>i!=null),"No máximo quatro downloads de capas em paralelo");foreach(var image in images)image.Dispose();
   int before=handler.Count;Assert(await service.LoadImageAsync("http://s4.anilist.co/test",CancellationToken.None)==null&&handler.Count==before,"URL não HTTPS rejeitada");
  }
 }
}
