using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AniLista {
public sealed class CoverService {
 public static readonly CoverService Shared=new CoverService(new HttpClient{Timeout=TimeSpan.FromSeconds(18)});
 public string Folder=Path.Combine(Path.GetTempPath(),"AniLista-capas");
 const int MaximumBytes=4000000,MemoryLimit=32000000;
 readonly HttpClient client;
 readonly SemaphoreSlim downloads=new SemaphoreSlim(4,4);
 readonly object sync=new object();
 readonly Dictionary<string,byte[]> memory=new Dictionary<string,byte[]>(StringComparer.Ordinal);
 readonly Queue<string> order=new Queue<string>();int memoryBytes;
 public CoverService(HttpClient http){client=http;}
 static bool TryUri(string url,out Uri uri){return Uri.TryCreate(url??"",UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host.EndsWith(".anilist.co",StringComparison.OrdinalIgnoreCase);}
 static Image Decode(byte[] bytes){try{using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream))return new Bitmap(image);}catch{return null;}}
 async Task<byte[]> Download(Uri uri,CancellationToken token){
  using(var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token)){
   if(!response.IsSuccessStatusCode)return null;
   if(response.Content.Headers.ContentLength.HasValue&&response.Content.Headers.ContentLength.Value>MaximumBytes)return null;
   using(var input=await response.Content.ReadAsStreamAsync())using(var output=new MemoryStream()){
    var buffer=new byte[16384];int count;
    while((count=await input.ReadAsync(buffer,0,buffer.Length,token))>0){if(output.Length+count>MaximumBytes)return null;output.Write(buffer,0,count);}
    return output.ToArray();
   }
  }
 }
 public async Task<Image> LoadImageAsync(string url,CancellationToken token){
  Uri uri;if(!TryUri(url,out uri))return null;
  await downloads.WaitAsync(token);
  try{var bytes=await Download(uri,token);return bytes==null?null:Decode(bytes);}catch(OperationCanceledException){throw;}catch{return null;}finally{downloads.Release();}
 }
 static string Key(Anime anime){using(var sha=SHA256.Create())return anime.CatalogId+"-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(anime.Cover??""))).Replace("-","").Substring(0,16);}
 public async Task<Image> LoadCoverAsync(Anime anime,CancellationToken token){
  Uri uri;if(anime==null||anime.CatalogId<=0||!TryUri(anime.Cover,out uri))return null;
  string key=Key(anime),folder=Folder,file=Path.Combine(folder,key+".jpg");
  await downloads.WaitAsync(token);
  try{
   token.ThrowIfCancellationRequested();byte[] bytes;
   lock(sync)memory.TryGetValue(key,out bytes);
   if(bytes==null)bytes=await Task.Run(()=>{try{var info=new FileInfo(file);return info.Exists&&info.Length<=MaximumBytes?File.ReadAllBytes(file):null;}catch{return null;}},token);
   Image image=bytes==null?null:Decode(bytes);
   if(image==null){
    bytes=await Download(uri,token);if(bytes==null)return null;image=Decode(bytes);if(image==null)return null;
    try{
     Directory.CreateDirectory(folder);string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
     try{await Task.Run(()=>File.WriteAllBytes(temp,bytes),token);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}finally{if(File.Exists(temp))File.Delete(temp);}
    }catch(OperationCanceledException){image.Dispose();throw;}catch{}
   }
   if(token.IsCancellationRequested){image.Dispose();token.ThrowIfCancellationRequested();}
   lock(sync){
    if(!memory.ContainsKey(key)){
     while(memoryBytes+bytes.Length>MemoryLimit&&order.Count>0){string old=order.Dequeue();memoryBytes-=memory[old].Length;memory.Remove(old);}
     memory[key]=bytes;order.Enqueue(key);memoryBytes+=bytes.Length;
    }
   }
   return image;
  }catch(OperationCanceledException){throw;}catch{return null;}finally{downloads.Release();}
 }
}
}
