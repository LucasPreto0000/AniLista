using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace AniLista {
public enum AnitsuMode { Disabled, AppLogin, BrowserExtension }
public enum AnitsuSearchState { Found, NotFound, LoginRequired, Unavailable }
public sealed class AnitsuCandidate {
 public string Name="",Path="",Kind="";
 public override string ToString(){return Name+" — "+Path;}
}
public sealed class AnitsuSearchResult {
 public AnitsuSearchState State;
 public List<AnitsuCandidate> Candidates=new List<AnitsuCandidate>();
 public string Message="";
 public static AnitsuSearchResult Error(AnitsuSearchState state,string message){return new AnitsuSearchResult{State=state,Message=message};}
}
public interface IAnitsuSearchProvider:IDisposable {
 Task<AnitsuSearchResult> SearchAsync(string title,CancellationToken token);
 Task OpenAsync(AnitsuCandidate candidate,CancellationToken token);
}
}
