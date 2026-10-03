using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace AniLista {
public sealed class AnitsuWebViewProvider:IAnitsuSearchProvider {
 readonly string folder;AnitsuWebViewForm form;bool disposed;
 public AnitsuWebViewProvider(string dataFolder){folder=dataFolder;}
 AnitsuWebViewForm Get(){if(disposed)throw new ObjectDisposedException("Anitsu");WebViewDependencies.Register();if(form==null||form.IsDisposed){form=new AnitsuWebViewForm(folder);var handle=form.Handle;}return form;}
 public async Task Connect(IWin32Window owner){await Get().Connect(owner);}
 public async Task Disconnect(){if(!System.IO.Directory.Exists(System.IO.Path.Combine(folder,"anitsu-profile")))return;var browser=Get();try{await browser.Disconnect();}finally{browser.CloseSession();if(form==browser)form=null;}}
 public void CloseSession(){if(form!=null){form.CloseSession();form=null;}}
 public async Task<AnitsuSearchResult> SearchAsync(string title,CancellationToken token){
  var browser=Get();try{return await browser.Search(title,token);}finally{if(!browser.Visible&&!browser.IsDisposed){browser.CloseSession();if(form==browser)form=null;}}
 }
 public Task OpenAsync(AnitsuCandidate candidate,CancellationToken token){token.ThrowIfCancellationRequested();if(!AnitsuApi.ValidCandidate(candidate))throw new ArgumentException("Resultado inválido.");Process.Start(new ProcessStartInfo(AnitsuApi.Cloud){UseShellExecute=true});return Task.FromResult(0);}
 public void Dispose(){if(disposed)return;disposed=true;CloseSession();}
}
}
