using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using AniLista;
class AnitsuWebViewSmoke {
 [STAThread]static int Main(){
  int exit=1;string folder=Path.Combine(Path.GetTempPath(),"AniLista-web-smoke-"+Guid.NewGuid().ToString("N"));WebViewDependencies.Register();Application.EnableVisualStyles();
  using(var owner=new Form{ShowInTaskbar=false,Opacity=0})using(var provider=new AnitsuWebViewProvider(folder)){
   owner.Shown+=async delegate{try{using(var timeout=new CancellationTokenSource(45000)){var result=await provider.SearchAsync("Lain",timeout.Token);Console.WriteLine("WebView2 live response: "+result.State+" "+result.Message);exit=result.State==AnitsuSearchState.LoginRequired?0:1;}}catch(Exception e){Console.WriteLine(e);exit=1;}finally{owner.Close();}};
   Application.Run(owner);
  }
  // WebView2 child processes may release profile files shortly after disposal.
  try{Directory.Delete(folder,true);}catch(IOException){}catch(UnauthorizedAccessException){}return exit;
 }
}
