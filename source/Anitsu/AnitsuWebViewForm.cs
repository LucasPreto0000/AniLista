using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace AniLista {
public sealed class AnitsuWebViewForm:DpiForm {
 readonly WebView2 view=new WebView2();readonly string profile;
 readonly string dataFolder;bool pageReady;string loginTitle="";
 AnitsuDownloaderBridge downloader;CancellationTokenSource currentSearch;
 readonly Dictionary<string,TaskCompletionSource<bool>> navigation=new Dictionary<string,TaskCompletionSource<bool>>();
 readonly Dictionary<string,TaskCompletionSource<AnitsuSearchResult>> pending=new Dictionary<string,TaskCompletionSource<AnitsuSearchResult>>();
 Task init;bool closing;readonly CancellationTokenSource lifetime=new CancellationTokenSource();
 public AnitsuWebViewForm(string folder){
  dataFolder=folder;profile=System.IO.Path.Combine(folder,"anitsu-profile");Text="Anitsu Downloader · AniLista";Size=new Size(1180,820);MinimumSize=Theme.S(this,880,620);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Background;Theme.DarkTitle(this);
  if(Theme.AppIcon!=null)Icon=Theme.AppIcon;ShowIcon=true;
  view.Dock=DockStyle.Fill;Controls.Add(view);FormClosing+=delegate{lifetime.Cancel();CancelSearch();};
 }
 public Task Initialize(){if(init==null)init=InitializeCore();return init;}
 async Task InitializeCore(){
  WebViewDependencies.PrepareLoader();var environment=await CoreWebView2Environment.CreateAsync(null,profile,null);await view.EnsureCoreWebView2Async(environment);
  view.CoreWebView2.Settings.AreDevToolsEnabled=false;view.CoreWebView2.Settings.IsWebMessageEnabled=true;view.ZoomFactor=0.9;
  await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(EmbeddedAnitsuAssets.Bootstrap);
  downloader=new AnitsuDownloaderBridge(view.CoreWebView2,dataFolder,delegate{});
  view.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){if(!AnitsuDownloadPolicy.IsNavigation(e.Uri))e.Cancel=true;else pageReady=false;};
  view.CoreWebView2.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;if(AnitsuDownloadPolicy.IsDownload(e.Uri)){var json=new JavaScriptSerializer();var ignored=view.CoreWebView2.ExecuteScriptAsync("var f=document.createElement('iframe');f.hidden=true;f.src="+json.Serialize(e.Uri)+";document.body.appendChild(f);");}else if(AnitsuDownloadPolicy.IsNavigation(e.Uri))view.CoreWebView2.Navigate(e.Uri);};
  view.CoreWebView2.WebMessageReceived+=delegate(object sender,CoreWebView2WebMessageReceivedEventArgs e){
   if(!AnitsuApi.IsSite(e.Source))return;
   try{var row=new JavaScriptSerializer{MaxJsonLength=AnitsuApi.MaxBytes+8192}.DeserializeObject(e.WebMessageAsJson) as Dictionary<string,object>;if(row==null)return;
    if(AnitsuApi.StringValue(row,"channel")=="anilista-cloud-typing"&&AnitsuDownloadPolicy.IsCloud(e.Source)){CancelSearch();return;}if(AnitsuApi.StringValue(row,"channel")=="anilista-cloud-ready"&&AnitsuDownloadPolicy.IsCloud(e.Source)){pageReady=true;if(loginTitle.Length>0){string resume=loginTitle;loginTitle="";BeginInvoke(new Action(async delegate{if(!Visible||closing)return;try{await FindAnime(resume,lifetime.Token);}catch(OperationCanceledException){}catch(Exception error){ShowStatus(error.Message);}}));}return;}string id=AnitsuApi.StringValue(row,"id");TaskCompletionSource<bool> nav;if(navigation.TryGetValue(id,out nav)){object ok;nav.TrySetResult(row.TryGetValue("ok",out ok)&&ok is bool&&(bool)ok);return;}TaskCompletionSource<AnitsuSearchResult> request;if(!pending.TryGetValue(id,out request))return;
    int code=Convert.ToInt32(row["status"]);request.TrySetResult(AnitsuApi.FromHttp(code,AnitsuApi.StringValue(row,"body")));
   }catch(Exception){}
  };
 }
 public void ShowStatus(string text){if(!IsDisposed&&!closing&&Visible)Notice.Tell(TopLevelControl??this,"Anitsu",text);}
 public CoreWebView2 Core{get{return view.CoreWebView2;}}
 public double BrowserZoomFactor{get{return view.ZoomFactor;}}
 void CancelSearch(){if(currentSearch!=null)currentSearch.Cancel();}
 public void SuspendSearch(){loginTitle="";CancelSearch();}
 public async Task ShowCloud(CancellationToken token){CancelSearch();using(var operation=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token)){await AnitsuAsync.Wait(Initialize().ContinueWith(t=>{t.GetAwaiter().GetResult();return true;},TaskScheduler.Default),operation.Token);await NavigateCloud(operation.Token);}}
 public async Task FindAnime(string title,CancellationToken token){CancelSearch();using(var operation=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token)){currentSearch=operation;try{await FindAnimeCore(title,operation.Token);}finally{if(currentSearch==operation)currentSearch=null;}}}
 async Task FindAnimeCore(string title,CancellationToken token){
  if(String.IsNullOrWhiteSpace(title)||title.Length>180){ShowStatus("Digite o nome do anime.");return;}
  loginTitle="";var result=await Search(title,token);token.ThrowIfCancellationRequested();
  if(result.State!=AnitsuSearchState.Found){if(result.State==AnitsuSearchState.LoginRequired){loginTitle=title;view.CoreWebView2.Navigate("https://anitsu.moe/");}else ShowStatus(result.State==AnitsuSearchState.NotFound?"Nenhuma pasta encontrada para "+title:result.Message);return;}
  var exact=AnitsuMatcher.Match(title,null,result.Candidates);AnitsuCandidate choice=null;if(exact.Count==1)choice=exact[0];else using(var choose=new AnitsuResultsForm(exact.Count>0?exact:result.Candidates)){if(choose.ShowDialog(TopLevelControl??this)==DialogResult.OK)choice=choose.Selected;}
  token.ThrowIfCancellationRequested();if(choice==null||IsDisposed)return;bool opened=false;try{opened=await OpenFolder(choice,token);}catch(TimeoutException){}token.ThrowIfCancellationRequested();if(!opened){Clipboard.SetText(choice.Path);ShowStatus("A pasta não abriu. O caminho foi copiado: "+choice.Path);}
 }
 async Task<bool> OpenFolder(AnitsuCandidate candidate,CancellationToken token){
  string id=Guid.NewGuid().ToString("N");var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);navigation.Add(id,completion);var json=new JavaScriptSerializer();
  using(token.Register(delegate{if(!IsDisposed&&IsHandleCreated)try{BeginInvoke(new Action(delegate{if(view.CoreWebView2!=null){var ignored=view.CoreWebView2.ExecuteScriptAsync("window.__aniListaNavCancel=window.__aniListaNavCancel||{};window.__aniListaNavCancel["+json.Serialize(id)+"]=true;");}}));}catch(InvalidOperationException){}}))
  try{string script=EmbeddedAnitsuAssets.Read("navigate.js")+"\nwindow.aniListaNavigate("+json.Serialize(candidate.Name)+","+json.Serialize(candidate.Path)+","+json.Serialize(id)+");";await view.CoreWebView2.ExecuteScriptAsync(script);return await AnitsuAsync.Wait(completion.Task,token);}finally{navigation.Remove(id);}
 }
 public async Task<AnitsuSearchResult> Search(string title,CancellationToken token){
  if(String.IsNullOrWhiteSpace(title)||title.Length>180)throw new ArgumentException("Título inválido.");
  await AnitsuAsync.Wait(Initialize().ContinueWith(t=>{t.GetAwaiter().GetResult();return true;},TaskScheduler.Default),token);
  await NavigateCloud(token);token.ThrowIfCancellationRequested();string id=Guid.NewGuid().ToString("N");var request=new TaskCompletionSource<AnitsuSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);pending.Add(id,request);
  try{var json=new JavaScriptSerializer();string script="(function(){window.aniListaSetQuery("+json.Serialize(title)+");const id="+json.Serialize(id)+",q="+json.Serialize(title)+";const c=new AbortController();const timer=setTimeout(()=>c.abort(),19000);fetch('/api/search?q='+encodeURIComponent(q),{credentials:'same-origin',signal:c.signal}).then(async r=>{const b=await r.text();window.chrome.webview.postMessage({id,status:r.status,body:b.length>524288?'':b});}).catch(()=>window.chrome.webview.postMessage({id,status:0,body:''})).finally(()=>clearTimeout(timer));})();";
   await view.CoreWebView2.ExecuteScriptAsync(script);return await AnitsuAsync.Wait(request.Task,token);
  }finally{pending.Remove(id);}
 }
 async Task NavigateCloud(CancellationToken token){
  if(pageReady&&AnitsuDownloadPolicy.IsCloud(view.CoreWebView2.Source))return;
  if(!AnitsuDownloadPolicy.IsCloud(view.CoreWebView2.Source))view.CoreWebView2.Navigate(AnitsuApi.Cloud);
  var clock=System.Diagnostics.Stopwatch.StartNew();
  while(!pageReady){token.ThrowIfCancellationRequested();if(closing)throw new OperationCanceledException();if(clock.ElapsedMilliseconds>=20000)throw new TimeoutException("Não foi possível carregar o Anitsu.");await Task.Delay(20,token);}
 }
 public async Task Connect(IWin32Window owner){Show(owner);Activate();CancelSearch();await Initialize();if(!IsDisposed)view.CoreWebView2.Navigate("https://anitsu.moe/");}
 public async Task Disconnect(){CancelSearch();await Initialize();await view.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);}
 public void CloseSession(){Close();Dispose();}
 protected override void Dispose(bool disposing){if(disposing&&!closing){closing=true;lifetime.Cancel();CancelSearch();foreach(var item in pending.Values.ToArray())item.TrySetCanceled();pending.Clear();foreach(var item in navigation.Values.ToArray())item.TrySetCanceled();navigation.Clear();if(downloader!=null)downloader.Dispose();}base.Dispose(disposing);}
}
}
