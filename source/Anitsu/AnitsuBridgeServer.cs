using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
namespace AniLista {
public sealed class AnitsuBridgeServer:IAnitsuSearchProvider {
 readonly object gate=new object();readonly Dictionary<string,TaskCompletionSource<Dictionary<string,object>>> pending=new Dictionary<string,TaskCompletionSource<Dictionary<string,object>>>();
 NamedPipeServerStream pipe;bool disposed,started,connected,enabled;
 public bool Connected{get{lock(gate)return connected;}}
 public void Start(){lock(gate){if(disposed)return;enabled=true;System.Threading.Monitor.PulseAll(gate);if(started)return;started=true;}Task.Run((Action)Listen);}
 public void Stop(){lock(gate){enabled=false;connected=false;if(pipe!=null)pipe.Dispose();foreach(var request in pending.Values)request.TrySetCanceled();pending.Clear();}}
 void Listen(){
  while(true){NamedPipeServerStream current=null;
   lock(gate){while(!enabled&&!disposed)System.Threading.Monitor.Wait(gate);if(disposed)return;}
   try{var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
    current=new NamedPipeServerStream(AnitsuNativeProtocol.PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security);
    lock(gate){if(disposed){current.Dispose();return;}if(!enabled){current.Dispose();continue;}pipe=current;}current.WaitForConnection();
    string raw;while((raw=AnitsuNativeProtocol.Read(current))!=null){var row=AnitsuNativeProtocol.Parse(raw);string type=AnitsuApi.StringValue(row,"type");
     lock(gate){if(type=="hello"){if(AnitsuApi.StringValue(row,"extension")!=ExtensionIdentity.Id)throw new FormatException("Extensão inválida.");connected=true;continue;}
      if(!connected||type!="result")throw new FormatException("Mensagem inválida.");string id=AnitsuApi.StringValue(row,"id");TaskCompletionSource<Dictionary<string,object>> request;if(pending.TryGetValue(id,out request))request.TrySetResult(row);
     }
    }
   }catch(Exception){lock(gate){if(enabled&&!disposed&&current==null)System.Threading.Monitor.Wait(gate,500);}}finally{if(current!=null)current.Dispose();lock(gate){connected=false;if(pipe==current)pipe=null;foreach(var request in pending.Values)request.TrySetException(new IOException("A extensão desconectou."));pending.Clear();}}
   lock(gate)if(disposed)return;
  }
 }
 async Task<Dictionary<string,object>> Request(string type,object payload,CancellationToken token){
  token.ThrowIfCancellationRequested();string id=Guid.NewGuid().ToString("N");var completion=new TaskCompletionSource<Dictionary<string,object>>(TaskCreationOptions.RunContinuationsAsynchronously);
  lock(gate){if(!connected||pipe==null)throw new IOException("Conecte a extensão do Anitsu no navegador.");pending.Add(id,completion);try{AnitsuNativeProtocol.Write(pipe,AnitsuNativeProtocol.Json(new{type=type,id=id,payload=payload}));}catch{pending.Remove(id);throw;}}
  try{return await AnitsuAsync.Wait(completion.Task,token);}finally{lock(gate){pending.Remove(id);if((token.IsCancellationRequested||!completion.Task.IsCompleted)&&connected&&pipe!=null)try{AnitsuNativeProtocol.Write(pipe,AnitsuNativeProtocol.Json(new{type="cancel",id=id}));}catch{}}}
 }
 public async Task<AnitsuSearchResult> SearchAsync(string title,CancellationToken token){if(String.IsNullOrWhiteSpace(title)||title.Length>180)throw new ArgumentException("Título inválido.");Start();
  if(!Connected)return AnitsuSearchResult.Error(AnitsuSearchState.Unavailable,"Conecte a extensão no Chrome ou Edge.");
  var reply=await Request("search",new{title=title},token);object status;int code=reply.TryGetValue("status",out status)?Convert.ToInt32(status):0;return AnitsuApi.FromHttp(code,AnitsuApi.StringValue(reply,"body"));
 }
 public async Task OpenAsync(AnitsuCandidate candidate,CancellationToken token){if(!AnitsuApi.ValidCandidate(candidate))throw new ArgumentException("Resultado inválido.");var reply=await Request("open",new{name=candidate.Name,path=candidate.Path},token);object value;if(!reply.TryGetValue("ok",out value)||!(value is bool)||(bool)value==false)throw new IOException("Não foi possível abrir a pasta no Anitsu. "+AnitsuApi.StringValue(reply,"message"));}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;Stop();System.Threading.Monitor.PulseAll(gate);}}
}
}
