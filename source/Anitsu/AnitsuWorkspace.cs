using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace AniLista {
public interface IAnitsuWorkspace:IDisposable {
 Task SearchAsync(Anime anime,IWin32Window owner);
 Task ShowAsync(IWin32Window owner);
}
public sealed class AnitsuWorkspace:IAnitsuWorkspace {
 readonly string folder;AnitsuWebViewForm window;CancellationTokenSource search;bool disposed;
 public AnitsuWorkspace(string dataFolder){folder=dataFolder;}
 AnitsuWebViewForm Get(IWin32Window owner){if(disposed)throw new ObjectDisposedException("Anitsu");WebViewDependencies.Register();if(window==null||window.IsDisposed){window=new AnitsuWebViewForm(folder);window.FormClosed+=delegate{if(search!=null)search.Cancel();};}var main=owner as MainForm;if(main==null)throw new InvalidOperationException("O Anitsu deve abrir dentro da biblioteca.");main.ShowAnitsu(window);return window;}
 public async Task SearchAsync(Anime anime,IWin32Window owner){if(anime==null)return;Cancel();search=new CancellationTokenSource();var current=search;var form=Get(owner);try{await form.FindAnime(anime.Title,current.Token);}catch(OperationCanceledException){}catch(Exception e){if(!form.IsDisposed)form.ShowStatus("Não foi possível pesquisar: "+e.Message);}finally{if(search==current){search=null;current.Dispose();}}}
 public async Task ShowAsync(IWin32Window owner){Cancel();var form=Get(owner);try{await form.ShowCloud(CancellationToken.None);}catch(Exception e){if(!form.IsDisposed)form.ShowStatus("Não foi possível abrir o Anitsu: "+e.Message);}}
 void Cancel(){if(search!=null){search.Cancel();search.Dispose();search=null;}}
 public void PauseSearch(){Cancel();if(window!=null&&!window.IsDisposed)window.SuspendSearch();}
 public void Dispose(){if(disposed)return;disposed=true;Cancel();if(window!=null&&!window.IsDisposed)window.CloseSession();window=null;}
}
}
