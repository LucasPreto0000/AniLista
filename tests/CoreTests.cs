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
  try{Backups(folder);CatalogTests().GetAwaiter().GetResult();CoverTests().GetAwaiter().GetResult();Console.WriteLine("PASS: cinco backups, restauração, validação, paginação, isolamento do cache, Retry-After, cancelamento e limite de downloads.");return 0;}
  catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
  finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
 }
 static void Backups(string folder){
  var store=new LibraryStore(folder);var anime=new Anime{Title="Teste 日本語",Status="watching",Total=50};
  for(int episode=0;episode<10;episode++){anime.Episode=episode;anime.Validate();store.Save(new List<Anime>{anime.Copy()});}
  Assert(Directory.GetFiles(folder,"biblioteca.json.bak*").Length==5,"Cinco versões de backup");
  var restored=new LibraryStore(folder);
  File.WriteAllText(store.FilePath,"corrompido");File.WriteAllText(store.BackupPath(0),"backup recente corrompido");
  var recovered=restored.Load();Assert(restored.Recovered&&recovered[0].Episode==7,"Recuperação usa o próximo backup válido");
  Assert(File.ReadAllText(store.FilePath).Contains("Teste"),"Arquivo principal restaurado imediatamente");
  recovered[0].Episode=10;restored.Save(recovered);
  File.WriteAllText(store.FilePath,"outra corrupção");
  Assert(new LibraryStore(folder).Load()[0].Episode==7,"Salvar depois da recuperação preserva o backup válido");
  Assert(Directory.GetFiles(folder,"*.corrompido-*").Length==2,"Arquivos corrompidos preservados");
  File.Delete(store.FilePath);Assert(new LibraryStore(folder).Load()[0].Episode==7,"Principal ausente recupera backup");
  var bad=recovered[0].Copy();bad.Status="inválido";bool rejected=false;
  try{store.Save(new List<Anime>{bad});}catch{rejected=true;}Assert(rejected,"Dados inválidos rejeitados antes de substituir biblioteca");
  Assert(new LibraryStore(folder).Load()[0].Episode==7,"Falha de validação preserva biblioteca atual");
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
