using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace AniLista {
static class Program {
 [STAThread]static int Main(string[] args){
  DpiForm.EnablePerMonitor();
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  if(args.Length>0&&args[0]=="--self-test")return SelfTest();
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
  Application.ThreadException+=delegate(object s,ThreadExceptionEventArgs e){try{Notice.Tell(null,"Algo deu errado","O AniLista encontrou um problema, mas sua biblioteca continua salva.\n\n"+e.Exception.Message);}catch{}};
  bool ownsMutex;using(var mutex=new Mutex(true,"Local\\AniLista-Desktop-"+Environment.UserName,out ownsMutex)){
   if(!ownsMutex){Notice.Tell(null,"O AniLista já está aberto","Confira a barra de tarefas.");return 0;}
   try{var store=new LibraryStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AniLista"));Application.Run(new MainForm(store,store.Load()));return 0;}
   catch(Exception ex){Notice.Tell(null,"Não foi possível abrir o AniLista",ex.Message);return 1;}
   finally{mutex.ReleaseMutex();}
  }
 }
 static int SelfTest(){
  string folder=Path.Combine(Path.GetTempPath(),"AniLista-test-"+Guid.NewGuid().ToString("N"));
  try{
   var store=new LibraryStore(folder);Require(store.Load().Count==0,"Biblioteca inicial vazia");
   var anime=new Anime{Title="Teste japonês 日本語",Status="watching",Episode=3,Total=12};anime.Validate();store.Save(new List<Anime>{anime});var loaded=store.Load();
   Require(loaded.Count==1&&loaded[0].Title==anime.Title&&loaded[0].Episode==3,"Persistência Unicode e episódio");
   Anime changed=anime.Copy();changed.Episode=4;changed.Validate();store.Save(new List<Anime>{changed});Require(store.Load()[0].Episode==4&&File.Exists(store.FilePath+".bak"),"Atualização atômica e backup");
   changed.Status="completed";changed.Validate();Require(changed.Episode==12,"Concluído usa total");changed.Status="planned";changed.Validate();Require(changed.Episode==0,"Planejado começa em zero");
   changed.Status="watching";changed.Episode=13;bool rejected=false;try{changed.Validate();}catch{rejected=true;}Require(rejected,"Episódio acima do total rejeitado");
   File.WriteAllText(store.FilePath,"corrompido");Require(store.Load()[0].Episode==3&&store.Recovered,"Recuperação de backup");store.Save(new List<Anime>());Require(store.Load().Count==0,"Remoção persistida");
   List<SearchResult> catalog=Catalog.Search("Naruto",CancellationToken.None).GetAwaiter().GetResult();Require(catalog.Any(a=>a.Anime.Title.ToUpper().Contains("NARUTO")),"Busca real no catálogo");
   Require(catalog.Any(a=>a.Thumb.Length>0),"Resultados trazem o ícone da capa");
   Image thumb=Catalog.LoadImage(catalog.First(a=>a.Thumb.Length>0).Thumb,CancellationToken.None).GetAwaiter().GetResult();Require(thumb!=null&&thumb.Width>0,"Download do ícone da capa");thumb.Dispose();
   Console.WriteLine("PASS: persistência, atualização, backup, recuperação, listas, episódios, busca no catálogo e ícones das capas.");return 0;
  }catch(Exception ex){Console.WriteLine("FAIL: "+ex);return 1;}
 }
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
}
