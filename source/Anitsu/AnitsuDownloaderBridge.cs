using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
namespace AniLista {
public sealed class AnitsuDownloaderBridge:IDisposable {
 sealed class Ticket {public string Id,Page,Url,Name,Temporary,Folder;public int Generation;public DateTime Created;public CoreWebView2DownloadOperation Operation;public bool Finished;}
 readonly CoreWebView2 core;readonly Action<string> status;readonly string settingsPath;
 readonly Dictionary<string,CancellationTokenSource> requests=new Dictionary<string,CancellationTokenSource>();
 readonly List<Ticket> downloads=new List<Ticket>();readonly System.Windows.Forms.Timer timer;
 readonly HttpClient http=new HttpClient(new HttpClientHandler{UseCookies=false,AllowAutoRedirect=false});
 int generation;bool disposed;public string Folder{get;private set;}
 public AnitsuDownloaderBridge(CoreWebView2 web,string dataFolder,Action<string> feedback){
  core=web;status=feedback;settingsPath=Path.Combine(dataFolder,"anitsu-download-folder.txt");Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");try{if(File.Exists(settingsPath)){string saved=File.ReadAllText(settingsPath);if(Path.IsPathRooted(saved))Folder=Path.GetFullPath(saved);}}catch(IOException){}
  http.Timeout=System.Threading.Timeout.InfiniteTimeSpan;core.WebMessageReceived+=Message;core.DownloadStarting+=DownloadStarting;core.NavigationStarting+=Navigation;core.WebResourceResponseReceived+=Response;
  timer=new System.Windows.Forms.Timer{Interval=1000};timer.Tick+=delegate{foreach(var item in downloads.Where(d=>d.Operation==null&&(DateTime.UtcNow-d.Created).TotalSeconds>30).ToArray())Finish(item,false,"timeout");};timer.Start();
 }
 public void SelectFolder(IWin32Window owner){using(var choose=new FolderBrowserDialog{Description="Onde salvar os downloads do Anitsu",SelectedPath=Folder})if(choose.ShowDialog(owner)==DialogResult.OK){Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));File.WriteAllText(settingsPath,choose.SelectedPath);Folder=choose.SelectedPath;status("Downloads em: "+Folder);}}
 void Navigation(object sender,CoreWebView2NavigationStartingEventArgs e){if(!AnitsuDownloadPolicy.IsNavigation(e.Uri))return;generation++;foreach(var source in requests.Values.ToArray())source.Cancel();foreach(var item in downloads.Where(d=>d.Operation==null).ToArray())Finish(item,false,"cancelled");}
 void Response(object sender,CoreWebView2WebResourceResponseReceivedEventArgs e){if(disposed||e.Response.StatusCode<400)return;foreach(var item in downloads.Where(d=>d.Operation==null&&d.Url==e.Request.Uri).ToArray())Finish(item,false,"HTTP "+e.Response.StatusCode,e.Response.StatusCode);}
 async void Message(object sender,CoreWebView2WebMessageReceivedEventArgs e){
  if(disposed||!AnitsuDownloadPolicy.IsCloud(e.Source)||!AnitsuDownloadPolicy.IsCloud(core.Source))return;
  Dictionary<string,object> row;try{if(e.WebMessageAsJson.Length>1048576)return;row=new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=12}.DeserializeObject(e.WebMessageAsJson) as Dictionary<string,object>;}catch(Exception){return;}
  if(row==null||AnitsuApi.StringValue(row,"channel")!="anilista-downloader")return;string id=AnitsuApi.StringValue(row,"id"),page=AnitsuApi.StringValue(row,"page"),kind=AnitsuApi.StringValue(row,"kind");Guid parsed;if(!Guid.TryParse(page,out parsed)||id.Length>100||!id.StartsWith(page+"-",StringComparison.Ordinal))return;
  int stamp=generation;
  if(kind=="abort"){CancellationTokenSource source;if(requests.TryGetValue(id,out source))source.Cancel();foreach(var item in downloads.Where(d=>d.Id==id).ToArray()){if(item.Operation!=null)item.Operation.Cancel();Finish(item,false,"cancelled");}return;}
  if(requests.ContainsKey(id)||downloads.Any(d=>d.Id==id))return;
  if(kind=="download"){
   string url=AnitsuApi.StringValue(row,"url");if(!AnitsuDownloadPolicy.IsDownload(url)||downloads.Count>=20){Reply(id,page,stamp,"error",0,"","Destino inválido ou muitos downloads.","");return;}
   var ticket=new Ticket{Id=id,Page=page,Url=new Uri(url).AbsoluteUri,Name=AnitsuDownloadPolicy.SafeName(AnitsuApi.StringValue(row,"name")),Folder=Folder,Created=DateTime.UtcNow,Generation=stamp};downloads.Add(ticket);Reply(id,page,stamp,"start",200,"","","");return;
  }
  if(kind!="request")return;string requestUrl=AnitsuApi.StringValue(row,"url"),method=AnitsuApi.StringValue(row,"method"),body=AnitsuApi.StringValue(row,"data");
  if(!AnitsuDownloadPolicy.IsRequest(requestUrl,method)||Encoding.UTF8.GetByteCount(body)>1048576||requests.Count>=12){Reply(id,page,stamp,"error",0,"","Pedido inválido.","");return;}
  int timeout=20000;object value;if(row.TryGetValue("timeout",out value))try{timeout=Math.Min(30000,Math.Max(1000,Convert.ToInt32(value)));}catch(Exception){}
  var cancel=new CancellationTokenSource(timeout);requests.Add(id,cancel);
  try{using(var request=new HttpRequestMessage(new HttpMethod(method),requestUrl)){
   if(method=="POST")request.Content=new StringContent(body,Encoding.UTF8,"application/json");object headers;
   if(row.TryGetValue("headers",out headers)){var list=headers as Dictionary<string,object>;if(list!=null&&list.Count<=20)foreach(var pair in list){string header=pair.Value as string;if(header==null||header.Length>8192||header.Contains("\r")||header.Contains("\n"))continue;if(pair.Key.Equals("Content-Type",StringComparison.OrdinalIgnoreCase)){if(request.Content!=null){request.Content.Headers.Remove("Content-Type");request.Content.Headers.TryAddWithoutValidation(pair.Key,header);}}else if(new[]{"accept","apikey","authorization","x-api-key"}.Contains(pair.Key.ToLowerInvariant()))request.Headers.TryAddWithoutValidation(pair.Key,header);}}
   using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token))using(var input=await response.Content.ReadAsStreamAsync())using(var output=new MemoryStream()){
    var bytes=new byte[8192];int got;while((got=await input.ReadAsync(bytes,0,bytes.Length,cancel.Token))>0){if(output.Length+got>1048576)throw new IOException("Resposta grande demais.");output.Write(bytes,0,got);}
    Reply(id,page,stamp,"load",(int)response.StatusCode,Encoding.UTF8.GetString(output.ToArray()),"",response.Headers.ToString()+response.Content.Headers.ToString());
   }
  }}catch(OperationCanceledException){Reply(id,page,stamp,"timeout",0,"","","");}catch(Exception){Reply(id,page,stamp,"error",0,"","network","");}finally{requests.Remove(id);cancel.Dispose();}
 }
 void DownloadStarting(object sender,CoreWebView2DownloadStartingEventArgs e){
  if(disposed||!AnitsuDownloadPolicy.IsDownload(e.DownloadOperation.Uri)){e.Cancel=true;return;}
  var item=downloads.FirstOrDefault(d=>d.Operation==null&&d.Url==new Uri(e.DownloadOperation.Uri).AbsoluteUri);
  if(item==null){item=new Ticket{Id="",Page="",Url=e.DownloadOperation.Uri,Name=AnitsuDownloadPolicy.SafeName(Path.GetFileName(e.ResultFilePath)),Folder=Folder,Generation=generation,Created=DateTime.UtcNow};downloads.Add(item);}
  try{item.Temporary=AnitsuDownloadPolicy.Reserve(item.Folder,item.Name);item.Operation=e.DownloadOperation;e.ResultFilePath=item.Temporary;e.Handled=true;
   item.Operation.StateChanged+=delegate{if(disposed||item.Finished)return;if(item.Operation.State==CoreWebView2DownloadState.Completed)Finish(item,true,"");else if(item.Operation.State==CoreWebView2DownloadState.Interrupted)Finish(item,false,item.Operation.InterruptReason.ToString());};
   item.Operation.BytesReceivedChanged+=delegate{if(!disposed&&!item.Finished)status("Baixando: "+item.Name+" · "+(item.Operation.BytesReceived/1048576.0).ToString("N1")+" MB");};status("Baixando: "+item.Name);
  }catch(Exception){e.Cancel=true;Finish(item,false,"Não foi possível criar o arquivo.");}
 }
 void Finish(Ticket item,bool success,string error,int failureCode=0){if(item.Finished)return;item.Finished=true;downloads.Remove(item);string installed="";
  try{if(success)installed=AnitsuDownloadPolicy.Complete(item.Temporary,item.Folder,item.Name);else if(item.Temporary!=null&&File.Exists(item.Temporary))File.Delete(item.Temporary);}catch(Exception){success=false;error="Não foi possível finalizar o arquivo.";}
  if(!disposed)status(success?"Concluído: "+installed:"Download interrompido: "+item.Name);Reply(item.Id,item.Page,item.Generation,success?"load":"error",success?200:failureCode,"",error,"");
 }
 void Reply(string id,string page,int stamp,string action,int code,string body,string error,string headers){if(disposed||stamp!=generation||id.Length==0||!AnitsuDownloadPolicy.IsCloud(core.Source))return;try{core.PostWebMessageAsJson(new JavaScriptSerializer{MaxJsonLength=3*1048576}.Serialize(new{channel="anilista-downloader",id=id,page=page,@event=action,status=code,body=body,error=error,headers=headers}));}catch(Exception){}}
 public void Dispose(){if(disposed)return;disposed=true;timer.Dispose();core.WebMessageReceived-=Message;core.DownloadStarting-=DownloadStarting;core.NavigationStarting-=Navigation;core.WebResourceResponseReceived-=Response;foreach(var source in requests.Values.ToArray())source.Cancel();foreach(var item in downloads.ToArray()){if(item.Operation!=null)item.Operation.Cancel();Finish(item,false,"cancelled");}http.Dispose();}
}
}
