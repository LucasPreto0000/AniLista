using System;
using System.Threading;
using System.Threading.Tasks;
namespace AniLista {
public static class AnitsuAsync {
 public static async Task<T> Wait<T>(Task<T> task,CancellationToken token){
  using(var delay=new CancellationTokenSource())using(var linked=CancellationTokenSource.CreateLinkedTokenSource(token,delay.Token)){
   var timeout=Task.Delay(20000,linked.Token);var winner=await Task.WhenAny(task,timeout);token.ThrowIfCancellationRequested();
   if(winner!=task)throw new TimeoutException("O Anitsu demorou demais para responder.");delay.Cancel();return await task;
  }
 }
}
}
