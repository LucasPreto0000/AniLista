using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
namespace AniLista {
public static class AnitsuNativeHost {
 public static bool TryRun(string[] args,out int exitCode){
  exitCode=0;if(args.Length==0||!args[0].StartsWith("chrome-extension://",StringComparison.Ordinal))return false;
  if(args[0]!=ExtensionIdentity.Origin){exitCode=1;return true;}
  bool diagnostics=args.Length>1&&args[1]=="--native-host-diagnostics";
  try{using(var pipe=new NamedPipeClientStream(".",AnitsuNativeProtocol.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous)){
   pipe.Connect(4000);var input=Console.OpenStandardInput();var output=Console.OpenStandardOutput();
   var incoming=Task.Run(delegate{try{string message;while((message=AnitsuNativeProtocol.Read(input))!=null)AnitsuNativeProtocol.Write(pipe,message);if(diagnostics)Console.Error.WriteLine("Native input EOF.");}catch(Exception e){if(diagnostics)Console.Error.WriteLine("Native input: "+e.GetType().Name+": "+e.Message);}finally{pipe.Dispose();}});
   var outgoing=Task.Run(delegate{try{string message;while((message=AnitsuNativeProtocol.Read(pipe))!=null)AnitsuNativeProtocol.Write(output,message);if(diagnostics)Console.Error.WriteLine("Native pipe EOF.");}catch(Exception e){if(diagnostics)Console.Error.WriteLine("Native output: "+e.GetType().Name+": "+e.Message);}finally{pipe.Dispose();}});
   Task.WaitAny(incoming,outgoing);
  }}catch(Exception e){if(diagnostics)Console.Error.WriteLine("Native setup: "+e.GetType().Name+": "+e.Message);exitCode=1;}return true;
 }
}
}
