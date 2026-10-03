using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace AniLista {
public static class WebViewDependencies {
 static bool registered;static readonly object gate=new object();
 static string folder;
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetDllDirectory(string path);
 public static void Register(){lock(gate){if(registered)return;registered=true;AppDomain.CurrentDomain.AssemblyResolve+=Resolve;}}
 static Assembly Resolve(object sender,ResolveEventArgs e){string name=new AssemblyName(e.Name).Name;
  if(name!="Microsoft.Web.WebView2.Core"&&name!="Microsoft.Web.WebView2.WinForms")return null;
  return Assembly.LoadFrom(Extract(name+".dll"));
 }
 static string Extract(string name){lock(gate){
  if(folder==null)folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AniLista","runtime","webview2-1.0.4258.31");
  Directory.CreateDirectory(folder);string path=Path.Combine(folder,name.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(path));
  using(var stream=typeof(WebViewDependencies).Assembly.GetManifestResourceStream("AniLista.WebView2."+name.Replace('/','.'))){
   if(stream==null)throw new FileNotFoundException("Dependência WebView2 ausente.");
   byte[] bytes;using(var memory=new MemoryStream()){stream.CopyTo(memory);bytes=memory.ToArray();}
   bool same=false;using(var sha=SHA256.Create()){if(File.Exists(path))same=Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)))==Convert.ToBase64String(sha.ComputeHash(bytes));}
   if(!same){string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temporary,bytes);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
  }return path;
 }}
 public static void PrepareLoader(){string path=Extract((Environment.Is64BitProcess?"x64":"x86")+"/WebView2Loader.dll");if(!SetDllDirectory(Path.GetDirectoryName(path)))throw new IOException("Não foi possível preparar WebView2.");}
}
}
