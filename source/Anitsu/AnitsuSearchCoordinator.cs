using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace AniLista {
public sealed class AnitsuSearchCoordinator:IDisposable {
 readonly Func<AnitsuMode> mode;readonly Func<AnitsuMode,IAnitsuSearchProvider> provider;
 readonly Action<Action> dispatch;readonly Action<string> feedback;readonly Func<IList<AnitsuCandidate>,Task<AnitsuCandidate>> choose;
 readonly Queue<Anime> queue=new Queue<Anime>();readonly HashSet<string> pending=new HashSet<string>();readonly HashSet<IAnitsuSearchProvider> used=new HashSet<IAnitsuSearchProvider>();
 CancellationTokenSource cancel=new CancellationTokenSource();TaskCompletionSource<bool> idle;bool running,disposed;
 public Task WhenIdle{get{return idle==null?Task.FromResult(true):idle.Task;}}
 public AnitsuCandidate LastCandidate{get;private set;}
 public event Action<AnitsuCandidate> ResultReady;
 public AnitsuSearchCoordinator(Func<AnitsuMode> getMode,Func<AnitsuMode,IAnitsuSearchProvider> getProvider,Action<Action> dispatcher,Action<string> status,Func<IList<AnitsuCandidate>,Task<AnitsuCandidate>> select){mode=getMode;provider=getProvider;dispatch=dispatcher;feedback=status;choose=select;}
 public void OnSavedAddition(Anime anime){
  if(disposed||anime==null||mode()==AnitsuMode.Disabled)return;
  if(queue.Count>=10){feedback("Anime salvo. Há muitas buscas pendentes no Anitsu.");return;}
  if(!pending.Add(anime.Id))return;queue.Enqueue(anime.Copy());
  if(!running){running=true;idle=new TaskCompletionSource<bool>();dispatch(delegate{Drain();});}
 }
 async void Drain(){
  try{while(!disposed&&queue.Count>0){var anime=queue.Dequeue();var token=cancel.Token;var selectedMode=mode();
   AnitsuCandidate opening=null;try{
    if(selectedMode==AnitsuMode.Disabled)continue;var current=provider(selectedMode);used.Add(current);feedback("Anime salvo. Procurando no Anitsu…");
    var result=await current.SearchAsync(anime.Title,token);token.ThrowIfCancellationRequested();if(disposed||mode()!=selectedMode)continue;
    if(result.State!=AnitsuSearchState.Found){feedback(result.State==AnitsuSearchState.NotFound?"Anime salvo. Não encontrado no Anitsu.":"Anime salvo. "+result.Message);continue;}
    var exact=AnitsuMatcher.Match(anime.Title,null,result.Candidates);AnitsuCandidate choice;
    if(exact.Count==1)choice=exact[0];else choice=await choose(exact.Count>0?exact:result.Candidates);
    token.ThrowIfCancellationRequested();if(choice==null||disposed||mode()!=selectedMode)continue;
    opening=choice;LastCandidate=choice;if(ResultReady!=null)ResultReady(choice);
    await current.OpenAsync(choice,token);token.ThrowIfCancellationRequested();feedback("Encontrado no Anitsu: "+choice.Name+" — "+choice.Path);
   }catch(OperationCanceledException){}catch(Exception){if(!token.IsCancellationRequested&&!disposed)feedback(opening==null?"Anime salvo. Não foi possível consultar o Anitsu agora.":"Anime salvo. Não foi possível abrir a pasta. Caminho: "+opening.Path);}
   finally{pending.Remove(anime.Id);}
  }}finally{running=false;if(idle!=null)idle.TrySetResult(true);}
 }
 public void Cancel(){cancel.Cancel();cancel.Dispose();cancel=new CancellationTokenSource();queue.Clear();pending.Clear();}
 public void Dispose(){if(disposed)return;disposed=true;Cancel();cancel.Cancel();foreach(var p in used)p.Dispose();}
}
}
