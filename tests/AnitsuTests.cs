using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AniLista;

class AnitsuTests {
 static int count;
 static void Assert(bool value,string message){if(!value)throw new Exception(message);count++;}
 static AnitsuCandidate Candidate(string name,string path){return new AnitsuCandidate{Name=name,Path=path};}
 static int Main(){
  string folder=Path.Combine(Path.GetTempPath(),"AniLista-anitsu-"+Guid.NewGuid().ToString("N"));
  try{
   var results=new[]{Candidate("Infinite Stratos","Anime/IS"),Candidate("Infinite Stratos 2","Anime/IS2")};
   Assert(AnitsuMatcher.Match("Infinite Stratos 2",new string[0],results).Single().Path=="Anime/IS2","SeasonNumbersStayDistinct");
   Assert(AnitsuMatcher.Match("JOÃO: O Herói!",null,new[]{Candidate("Joao O Heroi","Anime/Teste")}).Count==1,"PunctuationAndAccentsMatch");
   Assert(AnitsuMatcher.Match("Lain",null,new[]{Candidate("Lain","Anime/Lain"),Candidate("Lain","BD/Lain")}).Count==2,"DuplicatePathsRequireChoice");
   Assert(!AnitsuApi.ValidCandidate(Candidate("X","../../cookie.txt")),"Path traversal rejected");
   Assert(!AnitsuApi.ValidCandidate(Candidate("X","https://evil.example")),"External URL rejected");
   Assert(!AnitsuApi.ValidCandidate(Candidate("X",new string('a',2049))),"Oversized path rejected");
   Assert(AnitsuApi.Parse("{\"results\":[{\"name\":\"Lain\",\"path\":\"Anime/Lain\",\"kind\":\"anime\"}]}").Candidates.Single().Path=="Anime/Lain","API contract parsed");
   bool malformed=false;try{AnitsuApi.Parse("<html>login</html>");}catch(FormatException){malformed=true;}Assert(malformed,"HTML payload rejected");
   var store=new LibraryStore(folder);store.Save(new List<Anime>{new Anime{Title="Minha biblioteca"}});byte[] main=File.ReadAllBytes(store.FilePath),backup=File.ReadAllBytes(store.BackupPath(0));
   var settings=new AnitsuSettingsStore(folder);Assert(settings.Load()==AnitsuMode.Disabled,"Default disabled");settings.Save(AnitsuMode.AppLogin);Assert(settings.Load()==AnitsuMode.AppLogin,"Mode persisted");
   Assert(main.SequenceEqual(File.ReadAllBytes(store.FilePath))&&backup.SequenceEqual(File.ReadAllBytes(store.BackupPath(0))),"SettingsDoNotChangeLibrary");
   File.WriteAllText(settings.FilePath,"broken");Assert(settings.Load()==AnitsuMode.Disabled,"BrokenSettingsDefaultToDisabled");
   Assert(AnitsuApi.FromHttp(401,"{}").State==AnitsuSearchState.LoginRequired,"401 asks login");
   Assert(AnitsuApi.FromHttp(429,"{}").State==AnitsuSearchState.Unavailable,"Rate limit does not look like no results");
   Assert(AnitsuApi.FromHttp(500,"{}").State==AnitsuSearchState.Unavailable,"Server failure is not not-found");
   Assert(AnitsuApi.FromHttp(200,"{\"results\":[]}").State==AnitsuSearchState.NotFound,"Empty result handled");
   CoordinatorTests().GetAwaiter().GetResult();
   BridgeTests().GetAwaiter().GetResult();
   NativeHelperTests().GetAwaiter().GetResult();
   using(var frame=new MemoryStream()){
    AnitsuNativeProtocol.Write(frame,"{\"id\":\"test\",\"type\":\"hello\"}");frame.Position=0;Assert(AnitsuNativeProtocol.Read(frame).Contains("hello"),"Native frame roundtrip");Assert(AnitsuNativeProtocol.Read(frame)==null,"Clean EOF");
   }
   bool shortFrame=false;try{AnitsuNativeProtocol.Read(new MemoryStream(new byte[]{4,0,0,0,123}));}catch(EndOfStreamException){shortFrame=true;}Assert(shortFrame,"Truncated frame rejected");
   bool hugeFrame=false;try{AnitsuNativeProtocol.Read(new MemoryStream(new byte[]{255,255,255,127}));}catch(FormatException){hugeFrame=true;}Assert(hugeFrame,"Oversized frame rejected before allocation");
   Console.WriteLine("PASS: Anitsu "+count+" assertions");return 0;
  }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
 }
 static async Task NativeHelperTests(){
  int exit;Assert(AnitsuNativeHost.TryRun(new[]{"chrome-extension://untrusted/"},out exit)&&exit==1,"Untrusted native origin rejected");
  using(var bridge=new AnitsuBridgeServer()){
   bridge.Start();using(var helper=new Process{StartInfo=new ProcessStartInfo(typeof(AnitsuApi).Assembly.Location,ExtensionIdentity.Origin){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true}}){
    helper.Start();try{
     AnitsuNativeProtocol.Write(helper.StandardInput.BaseStream,AnitsuNativeProtocol.Json(new{type="hello",extension=ExtensionIdentity.Id}));Assert(SpinWait.SpinUntil(()=>bridge.Connected,5000),"Single EXE native helper handshake");
     var search=bridge.SearchAsync("Lain",CancellationToken.None);var outgoing=AnitsuNativeProtocol.Parse(AnitsuNativeProtocol.Read(helper.StandardOutput.BaseStream));
     AnitsuNativeProtocol.Write(helper.StandardInput.BaseStream,AnitsuNativeProtocol.Json(new{type="result",id=AnitsuApi.StringValue(outgoing,"id"),status=401,body="{}"}));Assert((await search).State==AnitsuSearchState.LoginRequired,"Native stdio transport preserves login state");
    }finally{bridge.Stop();helper.StandardInput.Close();}
    Assert(helper.WaitForExit(5000),"Native helper exits when application pipe closes");
   }
  }
 }
 static async Task BridgeTests(){
  using(var bridge=new AnitsuBridgeServer()){
   bridge.Start();using(var client=new NamedPipeClientStream(".",AnitsuNativeProtocol.PipeName,PipeDirection.InOut)){
    client.Connect(5000);AnitsuNativeProtocol.Write(client,AnitsuNativeProtocol.Json(new{type="hello",extension=ExtensionIdentity.Id}));Assert(SpinWait.SpinUntil(()=>bridge.Connected,3000),"Bridge handshake");
    var search=bridge.SearchAsync("Lain",CancellationToken.None);var request=AnitsuNativeProtocol.Parse(AnitsuNativeProtocol.Read(client));
    AnitsuNativeProtocol.Write(client,AnitsuNativeProtocol.Json(new{type="result",id=AnitsuApi.StringValue(request,"id"),status=200,body="{\"results\":[]}"}));Assert((await search).State==AnitsuSearchState.NotFound,"Authenticated transport contract");
    var cancel=new CancellationTokenSource();var canceled=bridge.SearchAsync("Lain",cancel.Token);request=AnitsuNativeProtocol.Parse(AnitsuNativeProtocol.Read(client));cancel.Cancel();bool stopped=false;try{await canceled;}catch(OperationCanceledException){stopped=true;}Assert(stopped,"IPC request cancellation");
    Assert(AnitsuApi.StringValue(AnitsuNativeProtocol.Parse(AnitsuNativeProtocol.Read(client)),"type")=="cancel","Browser receives cancellation");
    bridge.Stop();Assert(!bridge.Connected,"Changing mode disconnects extension");
   }
   bridge.Start();using(var next=new NamedPipeClientStream(".",AnitsuNativeProtocol.PipeName,PipeDirection.InOut)){next.Connect(5000);AnitsuNativeProtocol.Write(next,AnitsuNativeProtocol.Json(new{type="hello",extension=ExtensionIdentity.Id}));Assert(SpinWait.SpinUntil(()=>bridge.Connected,3000),"Bridge can reconnect after changing mode");}
  }
 }
 sealed class Provider:IAnitsuSearchProvider {
  public int Requests,Opens;public bool Disposed,FailOpen;public TaskCompletionSource<AnitsuSearchResult> Pending;
  public AnitsuSearchResult Result=new AnitsuSearchResult{State=AnitsuSearchState.Found,Candidates=new List<AnitsuCandidate>{Candidate("Lain","Anime/Lain")}};
  public Task<AnitsuSearchResult> SearchAsync(string title,CancellationToken token){Requests++;return Pending==null?Task.FromResult(Result):Pending.Task;}
  public Task OpenAsync(AnitsuCandidate candidate,CancellationToken token){token.ThrowIfCancellationRequested();Opens++;if(FailOpen)throw new IOException("Changed website UI");return Task.FromResult(0);}
  public void Dispose(){Disposed=true;}
 }
 static async Task CoordinatorTests(){
  var mode=AnitsuMode.Disabled;var provider=new Provider();var feedback=new List<string>();int choices=0;
  using(var coordinator=new AnitsuSearchCoordinator(()=>mode,m=>provider,a=>a(),s=>feedback.Add(s),items=>{choices++;return Task.FromResult(items[0]);})){
   coordinator.OnSavedAddition(new Anime{Title="Lain"});await coordinator.WhenIdle;Assert(provider.Requests==0,"DisabledModeMakesNoRequests");
   mode=AnitsuMode.AppLogin;var anime=new Anime{Title="Lain"};coordinator.OnSavedAddition(anime);await coordinator.WhenIdle;Assert(provider.Opens==1,"OneAdditionOpensOnce");
   provider.Result.Candidates.Add(Candidate("Lain","BD/Lain"));coordinator.OnSavedAddition(new Anime{Title="Lain"});await coordinator.WhenIdle;Assert(choices==1&&provider.Opens==2,"MultipleResultsRequireChoice");
   provider.Result=AnitsuSearchResult.Error(AnitsuSearchState.NotFound,"");coordinator.OnSavedAddition(new Anime{Title="Missing"});await coordinator.WhenIdle;Assert(provider.Opens==2&&feedback.Last().Contains("Não encontrado"),"NoResultDoesNotOpen");
   provider.Result=new AnitsuSearchResult{State=AnitsuSearchState.Found,Candidates=new List<AnitsuCandidate>{Candidate("Lain","Anime/Lain")}};provider.FailOpen=true;coordinator.OnSavedAddition(new Anime{Title="Lain"});await coordinator.WhenIdle;Assert(coordinator.LastCandidate.Path=="Anime/Lain"&&feedback.Last().Contains("Anime/Lain"),"OpeningFailurePreservesCopyablePath");provider.FailOpen=false;
   provider.Pending=new TaskCompletionSource<AnitsuSearchResult>();coordinator.OnSavedAddition(new Anime{Title="Lain"});var completion=coordinator.WhenIdle;coordinator.Cancel();provider.Pending.SetResult(new AnitsuSearchResult{State=AnitsuSearchState.Found,Candidates=new List<AnitsuCandidate>{Candidate("Lain","Anime/Lain")}});await completion;Assert(provider.Opens==3,"ModeChangeIgnoresLateResponse");
  }
  Assert(provider.Disposed,"WindowCloseDisposesProvider");
 }
}
