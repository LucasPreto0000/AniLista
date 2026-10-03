using System;
using System.IO;
using System.Linq;
namespace AniLista {
public static class AnitsuDownloadPolicy {
 public static bool IsCloud(string url){Uri uri;return Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="nuvem.anitsu.moe"&&uri.IsDefaultPort&&String.IsNullOrEmpty(uri.UserInfo);}
 public static bool IsDownload(string url){Uri uri;return url!=null&&url.Length<=4096&&IsCloud(url)&&Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.AbsolutePath=="/api/download"&&AnitsuApi.ValidCandidate(new AnitsuCandidate{Name="download",Path=System.Web.HttpUtility.ParseQueryString(uri.Query)["path"]});}
 public static bool IsRequest(string url,string method){
  Uri uri;if(url==null||url.Length>4096||!Uri.TryCreate(url,UriKind.Absolute,out uri)||!String.IsNullOrEmpty(uri.UserInfo)||(method!="GET"&&method!="POST"))return false;
  if(uri.Scheme=="http"&&uri.Host=="127.0.0.1")return (uri.AbsolutePath=="/ping"&&method=="POST")||(uri.AbsolutePath=="/queues"&&method=="GET")||(uri.AbsolutePath=="/start-headless-download"&&method=="POST");
  if(uri.Scheme!="https"||!uri.IsDefaultPort)return false;
  if(uri.Host=="graphql.anilist.co")return uri.AbsolutePath=="/"&&method=="POST";
  return uri.Host=="qzrxxwizigfdcpmwkztq.supabase.co"&&uri.AbsolutePath=="/auth/v1/token"&&method=="POST"&&System.Web.HttpUtility.ParseQueryString(uri.Query)["grant_type"]=="refresh_token";
 }
 public static bool IsNavigation(string url){Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.IsDefaultPort||!String.IsNullOrEmpty(uri.UserInfo))return false;return AnitsuApi.IsSite(url)||uri.Host=="discord.com"||uri.Host=="qzrxxwizigfdcpmwkztq.supabase.co";}
 public static string SafeName(string name){name=Path.GetFileName((name??"").Replace('/',Path.DirectorySeparatorChar));var invalid=Path.GetInvalidFileNameChars();name=new string(name.Select(c=>invalid.Contains(c)||Char.IsControl(c)?'_':c).ToArray()).Trim().TrimEnd('.');if(name.Length==0)name="download";if(name.Length>180)name=name.Substring(0,Char.IsHighSurrogate(name[179])?179:180);string stem=Path.GetFileNameWithoutExtension(name).ToUpperInvariant();if(new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem))name="_"+name;return name;}
 public static string Reserve(string folder,string name){Directory.CreateDirectory(folder);string path=Path.Combine(Path.GetFullPath(folder),SafeName(name)+"."+Guid.NewGuid().ToString("N")+".part");using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write)){}return path;}
 public static string Complete(string temporary,string folder,string name){name=SafeName(name);for(int n=0;n<1000;n++){string target=Path.Combine(folder,n==0?name:Path.GetFileNameWithoutExtension(name)+" ("+n+")"+Path.GetExtension(name));try{File.Move(temporary,target);return target;}catch(IOException){if(!File.Exists(target))throw;}}throw new IOException("Muitos arquivos com o mesmo nome.");}
}
}
