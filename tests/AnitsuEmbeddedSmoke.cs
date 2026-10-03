using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using AniLista;
class AnitsuEmbeddedSmoke {
 [STAThread]static int Main(){WebViewDependencies.Register();return Run();}
 static int Run(){
  string folder=Path.Combine(Path.GetTempPath(),"AniLista-embedded-smoke-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);int exit=1;
  var store=new LibraryStore(Path.Combine(folder,"library"));using(var owner=new MainForm(store,store.Load()){ShowInTaskbar=false,Opacity=0}){
   owner.Shown+=async delegate{try{await Check(owner,folder);exit=0;Console.WriteLine("PASS: real WebView2, bundled panel, search/folder, native download, collision preservation and HTTP 401 callback");}catch(Exception error){Console.WriteLine("FAIL: "+error);}finally{owner.Close();}};
   Application.Run(owner);
  }
  try{Directory.Delete(folder,true);}catch(IOException){}catch(UnauthorizedAccessException){}return exit;
 }
 static async Task Check(MainForm owner,string folder){
  string destination=Path.Combine(folder,"downloads");Directory.CreateDirectory(destination);File.WriteAllText(Path.Combine(destination,"Lain.mkv"),"keep");File.WriteAllText(Path.Combine(folder,"anitsu-download-folder.txt"),destination);
  using(var form=new AnitsuWebViewForm(folder))using(var timeout=new CancellationTokenSource(60000)){
   typeof(MainForm).GetMethod("ShowAnitsu",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(owner,new object[]{form});Assert(!form.TopLevel&&form.TopLevelControl==owner,"Browser hosted in main window");await form.Initialize();var core=form.Core;
   core.AddWebResourceRequestedFilter("https://nuvem.anitsu.moe/*",CoreWebView2WebResourceContext.All);
   int pages=0;
   core.WebResourceRequested+=delegate(object sender,CoreWebView2WebResourceRequestedEventArgs e){
    var uri=new Uri(e.Request.Uri);string body="{\"files\":[]}",headers="Content-Type: application/json",reason="OK";int code=200;
    if(uri.AbsolutePath=="/"){pages++;headers="Content-Type: text/html; charset=utf-8";body="<!doctype html><html><head></head><body><nav><button>Home</button></nav><input placeholder='Buscar pastas...'><button onclick=\"document.querySelector('nav').innerHTML='<button>Home</button><div>Anime</div><div>Lain</div>'\"><span>Anime/Lain</span></button></body></html>";}
    else if(uri.AbsolutePath=="/api/search")body="{\"results\":[{\"name\":\"Lain\",\"path\":\"Anime/Lain\",\"kind\":\"directory\"}]}";
    else if(uri.AbsolutePath=="/api/download"){if(uri.Query.Contains("expired")){code=401;reason="Unauthorized";body="login";}else{body="test-download-bytes";headers="Content-Type: application/octet-stream\r\nContent-Disposition: attachment; filename=Lain.mkv";}}
    e.Response=core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(body)),code,reason,headers);
   };
   await form.ShowCloud(timeout.Token);
   for(int n=0;n<30;n++){if(await core.ExecuteScriptAsync("!!document.getElementById('anu-panel')")=="true")break;await Task.Delay(100,timeout.Token);}
   Assert(await core.ExecuteScriptAsync("!!document.getElementById('anu-panel')")=="true","Exact bundled userscript panel mounted");
   Assert(await core.ExecuteScriptAsync("document.getElementById('anu-idm-label').hidden")=="true","Unsupported browser extension mode hidden");
   Assert(await core.ExecuteScriptAsync("getComputedStyle(document.getElementById('anu-idm-label')).display === 'none'")=="true","Unsupported mode visually hidden");
   await form.FindAnime("Lain",timeout.Token);
   Assert(await core.ExecuteScriptAsync("document.querySelector('input[placeholder^=Buscar]').value === 'Lain'")=="true","Anime name filled automatically");
   await form.FindAnime("Lain",timeout.Token);Assert(pages==1,"Repeated searches reuse loaded Cloud without reload");
   await core.ExecuteScriptAsync("document.querySelector('input[placeholder^=Buscar]').remove();setTimeout(()=>{const input=document.createElement('input');input.placeholder='Buscar pastas...';document.body.appendChild(input);},150);");
   await form.FindAnime("Lain",timeout.Token);Assert(await core.ExecuteScriptAsync("document.querySelector('input[placeholder^=Buscar]').value === 'Lain'")=="true","Queued title survives temporary search input removal");Assert(pages==1,"Folder transition does not force full reload");
   Assert(await core.ExecuteScriptAsync("document.querySelector('nav').textContent.includes('Anime')")=="true","Found folder opened and breadcrumb confirmed");
   var complete=new TaskCompletionSource<string>();
   core.WebMessageReceived+=delegate(object sender,CoreWebView2WebMessageReceivedEventArgs e){if(e.WebMessageAsJson.Contains("smokeResult"))complete.TrySetResult(e.WebMessageAsJson);};
   await core.ExecuteScriptAsync("GM_download({url:'https://nuvem.anitsu.moe/api/download?path=Anime%2FLain.mkv',name:'Lain.mkv',onload:()=>chrome.webview.postMessage({smokeResult:'done'}),onerror:e=>chrome.webview.postMessage({smokeResult:'error',code:e.error})});");
   string result=await AnitsuAsync.Wait(complete.Task,timeout.Token);Assert(result.Contains("done"),"Native download completed: "+result);
   Assert(File.ReadAllText(Path.Combine(destination,"Lain.mkv"))=="keep","Existing file preserved");Assert(File.ReadAllText(Path.Combine(destination,"Lain (1).mkv"))=="test-download-bytes","Browser stream committed before callback");Assert(Directory.GetFiles(destination,"*.part").Length==0,"Temporary download cleared");
   complete=new TaskCompletionSource<string>();
   await core.ExecuteScriptAsync("GM_download({url:'https://nuvem.anitsu.moe/api/download?path=expired',name:'expired.mkv',onload:()=>chrome.webview.postMessage({smokeResult:'unexpected'}),onerror:e=>chrome.webview.postMessage({smokeResult:'expired',code:e.error})});");
   result=await AnitsuAsync.Wait(complete.Task,timeout.Token);Assert(result.Contains("expired")&&result.Contains("401"),"HTTP auth status forwarded: "+result);Assert(!File.Exists(Path.Combine(destination,"expired.mkv")),"Failed response not saved");
   form.CloseSession();
  }
 }
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
}
