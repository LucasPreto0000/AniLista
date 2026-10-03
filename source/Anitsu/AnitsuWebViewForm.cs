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
 readonly string dataFolder;readonly TextBox query;readonly Label feedback;readonly RoundButton copy;
 AnitsuDownloaderBridge downloader;string foundPath="";CancellationTokenSource currentSearch;
 readonly Dictionary<string,TaskCompletionSource<bool>> navigation=new Dictionary<string,TaskCompletionSource<bool>>();
 readonly Dictionary<string,TaskCompletionSource<AnitsuSearchResult>> pending=new Dictionary<string,TaskCompletionSource<AnitsuSearchResult>>();
 Task init;bool closing;readonly CancellationTokenSource lifetime=new CancellationTokenSource();
 public AnitsuWebViewForm(string folder){
  dataFolder=folder;profile=System.IO.Path.Combine(folder,"anitsu-profile");Text="Anitsu Downloader · AniLista";Size=new Size(1180,820);MinimumSize=Theme.S(this,880,620);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Background;Theme.DarkTitle(this);
  var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=104,Padding=new Padding(8),BackColor=Theme.Sidebar,WrapContents=true};
  var login=Theme.Button(this,"Entrar no Anitsu");login.Width=160;login.Click+=async delegate{try{CancelSearch();await Initialize();if(!IsDisposed)view.CoreWebView2.Navigate("https://anitsu.moe/");}catch(Exception e){ShowStatus(e.Message);}};
  var cloud=Theme.Button(this,"Cloud");cloud.Width=90;cloud.Click+=async delegate{try{await ShowCloud(CancellationToken.None);}catch(Exception e){ShowStatus(e.Message);}};
  query=Theme.TextBox(this);query.MaxLength=180;var input=Theme.Wrap(this,query,0,0,260,40);input.Margin=new Padding(4);Theme.Cue(query,"Nome do anime");
  var search=Theme.Button(this,"Pesquisar",true);search.Width=120;search.Click+=async delegate{try{await FindAnime(query.Text,CancellationToken.None);}catch(OperationCanceledException){}catch(Exception e){ShowStatus(e.Message);}};
  var destination=Theme.Button(this,"Pasta dos downloads");destination.Width=190;destination.Click+=async delegate{try{await Initialize();downloader.SelectFolder(this);}catch(Exception e){ShowStatus(e.Message);}};
  copy=Theme.Button(this,"Copiar caminho");copy.Width=145;copy.Enabled=false;copy.Click+=delegate{if(foundPath.Length>0)Clipboard.SetText(foundPath);};
  var logout=Theme.Button(this,"Sair da conta");logout.Width=145;logout.Click+=async delegate{try{await Disconnect();view.CoreWebView2.Navigate("https://anitsu.moe/");ShowStatus("Sessão encerrada.");}catch(Exception e){ShowStatus(e.Message);}};
  bar.Controls.Add(login);bar.Controls.Add(cloud);bar.Controls.Add(input);bar.Controls.Add(search);bar.Controls.Add(destination);bar.Controls.Add(copy);bar.Controls.Add(logout);
  feedback=Theme.Label(this,"Anitsu Downloader 1.6.8 integrado · entre na sua conta para pesquisar e baixar",10,Theme.Muted);feedback.Dock=DockStyle.Bottom;feedback.Height=48;feedback.Padding=new Padding(8);feedback.AutoEllipsis=true;
  view.Dock=DockStyle.Fill;Controls.Add(view);Controls.Add(feedback);Controls.Add(bar);FormClosing+=delegate{lifetime.Cancel();CancelSearch();};
 }
 public Task Initialize(){if(init==null)init=InitializeCore();return init;}
 async Task InitializeCore(){
  WebViewDependencies.PrepareLoader();var environment=await CoreWebView2Environment.CreateAsync(null,profile,null);await view.EnsureCoreWebView2Async(environment);
  view.CoreWebView2.Settings.AreDevToolsEnabled=false;view.CoreWebView2.Settings.IsWebMessageEnabled=true;
  await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(EmbeddedAnitsuAssets.Bootstrap);
  downloader=new AnitsuDownloaderBridge(view.CoreWebView2,dataFolder,ShowStatus);
  view.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){if(!AnitsuDownloadPolicy.IsNavigation(e.Uri))e.Cancel=true;};
  view.CoreWebView2.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;if(AnitsuDownloadPolicy.IsDownload(e.Uri)){var json=new JavaScriptSerializer();var ignored=view.CoreWebView2.ExecuteScriptAsync("var f=document.createElement('iframe');f.hidden=true;f.src="+json.Serialize(e.Uri)+";document.body.appendChild(f);");}else if(AnitsuDownloadPolicy.IsNavigation(e.Uri))view.CoreWebView2.Navigate(e.Uri);};
  view.CoreWebView2.WebMessageReceived+=delegate(object sender,CoreWebView2WebMessageReceivedEventArgs e){
   if(!AnitsuApi.IsSite(e.Source))return;
   try{var row=new JavaScriptSerializer{MaxJsonLength=AnitsuApi.MaxBytes+8192}.DeserializeObject(e.WebMessageAsJson) as Dictionary<string,object>;if(row==null)return;
    string id=AnitsuApi.StringValue(row,"id");TaskCompletionSource<bool> nav;if(navigation.TryGetValue(id,out nav)){object ok;nav.TrySetResult(row.TryGetValue("ok",out ok)&&ok is bool&&(bool)ok);return;}TaskCompletionSource<AnitsuSearchResult> request;if(!pending.TryGetValue(id,out request))return;
    int code=Convert.ToInt32(row["status"]);request.TrySetResult(AnitsuApi.FromHttp(code,AnitsuApi.StringValue(row,"body")));
   }catch(Exception){}
  };
 }
 public void ShowStatus(string text){if(!IsDisposed)feedback.Text=text;}
 public CoreWebView2 Core{get{return view.CoreWebView2;}}
 void CancelSearch(){if(currentSearch!=null)currentSearch.Cancel();}
 public async Task ShowCloud(CancellationToken token){CancelSearch();using(var operation=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token)){await AnitsuAsync.Wait(Initialize().ContinueWith(t=>{t.GetAwaiter().GetResult();return true;},TaskScheduler.Default),operation.Token);await NavigateCloud(operation.Token);}}
 public async Task FindAnime(string title,CancellationToken token){CancelSearch();using(var operation=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token)){currentSearch=operation;try{await FindAnimeCore(title,operation.Token);}finally{if(currentSearch==operation)currentSearch=null;}}}
 async Task FindAnimeCore(string title,CancellationToken token){
  if(String.IsNullOrWhiteSpace(title)||title.Length>180){ShowStatus("Digite o nome do anime.");return;}
  query.Text=title;foundPath="";copy.Enabled=false;ShowStatus("Pesquisando "+title+" no Anitsu…");var result=await Search(title,token);token.ThrowIfCancellationRequested();
  if(result.State!=AnitsuSearchState.Found){if(result.State==AnitsuSearchState.LoginRequired){ShowStatus("Entre na sua conta e clique em Pesquisar para tentar novamente.");view.CoreWebView2.Navigate("https://anitsu.moe/");}else ShowStatus(result.State==AnitsuSearchState.NotFound?"Nenhuma pasta encontrada para "+title:result.Message);return;}
  var exact=AnitsuMatcher.Match(title,null,result.Candidates);AnitsuCandidate choice=null;if(exact.Count==1)choice=exact[0];else using(var choose=new AnitsuResultsForm(exact.Count>0?exact:result.Candidates)){if(choose.ShowDialog(this)==DialogResult.OK)choice=choose.Selected;}
  token.ThrowIfCancellationRequested();if(choice==null||IsDisposed)return;foundPath=choice.Path;copy.Enabled=true;ShowStatus("Encontrado: "+foundPath);bool opened=false;try{opened=await OpenFolder(choice,token);}catch(TimeoutException){}token.ThrowIfCancellationRequested();ShowStatus(opened?"Pasta aberta: "+foundPath+" · selecione os arquivos no Anitsu Downloader":"Encontrado: "+foundPath+" · use Copiar caminho se a navegação do site falhar.");
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
 public async Task Connect(IWin32Window owner){Show(owner);Activate();CancelSearch();await Initialize();if(!IsDisposed)view.CoreWebView2.Navigate("https://anitsu.moe/");}
 public async Task Disconnect(){CancelSearch();await Initialize();await view.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);}
 public void CloseSession(){Close();Dispose();}
 protected override void Dispose(bool disposing){if(disposing&&!closing){closing=true;lifetime.Cancel();CancelSearch();foreach(var item in pending.Values.ToArray())item.TrySetCanceled();pending.Clear();foreach(var item in navigation.Values.ToArray())item.TrySetCanceled();navigation.Clear();if(downloader!=null)downloader.Dispose();}base.Dispose(disposing);}
}
}
