using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace AniLista {
public sealed class AnitsuWebViewForm:DpiForm {
 readonly WebView2 view=new WebView2();readonly string profile;
 readonly Dictionary<string,TaskCompletionSource<AnitsuSearchResult>> pending=new Dictionary<string,TaskCompletionSource<AnitsuSearchResult>>();
 Task init;bool closing;
 public AnitsuWebViewForm(string folder){
  profile=System.IO.Path.Combine(folder,"anitsu-profile");Text="Conectar ao Anitsu";Size=new Size(1000,740);BackColor=Theme.Background;Theme.DarkTitle(this);
  var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(6),BackColor=Theme.Sidebar};
  var cloud=Theme.Button(this,"Abrir Anitsu Cloud");cloud.Width=210;cloud.Click+=async delegate{try{await Initialize();view.CoreWebView2.Navigate(AnitsuApi.Cloud);}catch(Exception e){Notice.Tell(this,"Anitsu",e.Message);}};
  var done=Theme.Button(this,"Concluir login",true);done.Width=170;done.Click+=delegate{CloseSession();};bar.Controls.Add(cloud);bar.Controls.Add(done);
  view.Dock=DockStyle.Fill;Controls.Add(view);Controls.Add(bar);
 }
 public Task Initialize(){if(init==null)init=InitializeCore();return init;}
 async Task InitializeCore(){
  WebViewDependencies.PrepareLoader();var environment=await CoreWebView2Environment.CreateAsync(null,profile,null);await view.EnsureCoreWebView2Async(environment);
  view.CoreWebView2.Settings.AreDevToolsEnabled=false;view.CoreWebView2.Settings.IsWebMessageEnabled=true;
  view.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){if(!AnitsuApi.IsSite(e.Uri))e.Cancel=true;};
  view.CoreWebView2.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;if(AnitsuApi.IsSite(e.Uri))view.CoreWebView2.Navigate(e.Uri);};
  view.CoreWebView2.WebMessageReceived+=delegate(object sender,CoreWebView2WebMessageReceivedEventArgs e){
   if(!AnitsuApi.IsSite(e.Source))return;
   try{var row=new JavaScriptSerializer{MaxJsonLength=AnitsuApi.MaxBytes+8192}.DeserializeObject(e.WebMessageAsJson) as Dictionary<string,object>;if(row==null)return;
    string id=AnitsuApi.StringValue(row,"id");TaskCompletionSource<AnitsuSearchResult> request;if(!pending.TryGetValue(id,out request))return;
    int code=Convert.ToInt32(row["status"]);request.TrySetResult(AnitsuApi.FromHttp(code,AnitsuApi.StringValue(row,"body")));
   }catch(Exception){}
  };
 }
 public async Task<AnitsuSearchResult> Search(string title,CancellationToken token){
  if(String.IsNullOrWhiteSpace(title)||title.Length>180)throw new ArgumentException("Título inválido.");
  await AnitsuAsync.Wait(Initialize().ContinueWith(t=>{t.GetAwaiter().GetResult();return true;},TaskScheduler.Default),token);
  await NavigateCloud(token);token.ThrowIfCancellationRequested();string id=Guid.NewGuid().ToString("N");var request=new TaskCompletionSource<AnitsuSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);pending.Add(id,request);
  try{var json=new JavaScriptSerializer();string script="(function(){const id="+json.Serialize(id)+",q="+json.Serialize(title)+";const c=new AbortController();const timer=setTimeout(()=>c.abort(),19000);fetch('/api/search?q='+encodeURIComponent(q),{credentials:'same-origin',signal:c.signal}).then(async r=>{const b=await r.text();window.chrome.webview.postMessage({id,status:r.status,body:b.length>524288?'':b});}).catch(()=>window.chrome.webview.postMessage({id,status:0,body:''})).finally(()=>clearTimeout(timer));})();";
   await view.CoreWebView2.ExecuteScriptAsync(script);return await AnitsuAsync.Wait(request.Task,token);
  }finally{pending.Remove(id);}
 }
 async Task NavigateCloud(CancellationToken token){
  var ready=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);EventHandler<CoreWebView2NavigationCompletedEventArgs> handler=delegate(object s,CoreWebView2NavigationCompletedEventArgs e){if(e.IsSuccess)ready.TrySetResult(true);else ready.TrySetException(new Exception("Não foi possível carregar o Anitsu."));};
  view.CoreWebView2.NavigationCompleted+=handler;
  try{view.CoreWebView2.Navigate(AnitsuApi.Cloud);await AnitsuAsync.Wait(ready.Task,token);if(!AnitsuApi.IsSite(view.CoreWebView2.Source)||new Uri(view.CoreWebView2.Source).Host!="nuvem.anitsu.moe")throw new Exception("Entre no Anitsu primeiro.");}
  finally{view.CoreWebView2.NavigationCompleted-=handler;}
 }
 public async Task Connect(IWin32Window owner){Show(owner);Activate();await Initialize();view.CoreWebView2.Navigate("https://anitsu.moe/");}
 public async Task Disconnect(){await Initialize();await view.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);}
 public void CloseSession(){closing=true;foreach(var item in pending.Values)item.TrySetCanceled();pending.Clear();Close();Dispose();}
 protected override void Dispose(bool disposing){if(disposing&&!closing){closing=true;foreach(var item in pending.Values)item.TrySetCanceled();pending.Clear();}base.Dispose(disposing);}
}
}
