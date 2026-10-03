using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace AniLista {
public static class AnitsuApi {
 public const string Cloud="https://nuvem.anitsu.moe/";
 public const int MaxBytes=512*1024;
 public static AnitsuSearchResult FromHttp(int status,string body){
  if(status==401||status==403)return AnitsuSearchResult.Error(AnitsuSearchState.LoginRequired,"Entre na sua conta no Anitsu.");
  if(status!=200)return AnitsuSearchResult.Error(AnitsuSearchState.Unavailable,"O Anitsu está indisponível agora (HTTP "+status+").");
  try{return Parse(body);}catch(FormatException){return AnitsuSearchResult.Error(AnitsuSearchState.Unavailable,"O Anitsu devolveu uma resposta inválida.");}
 }
 public static bool ValidCandidate(AnitsuCandidate candidate){
  if(candidate==null||String.IsNullOrWhiteSpace(candidate.Name)||candidate.Name.Length>500||String.IsNullOrWhiteSpace(candidate.Path)||candidate.Path.Length>2048)return false;
  return !candidate.Path.Any(c=>Char.IsControl(c)||c=='\\'||c==':')&&!candidate.Path.Split('/').Any(p=>p==".."||p==".");
 }
 public static AnitsuSearchResult Parse(string text){
  if(text==null||Encoding.UTF8.GetByteCount(text)>MaxBytes)throw new FormatException("Resposta do Anitsu grande demais.");
  try{
   var json=new JavaScriptSerializer{MaxJsonLength=MaxBytes};var root=json.DeserializeObject(text) as Dictionary<string,object>;
   object value;var rows=root!=null&&root.TryGetValue("results",out value)?value as object[]:null;
   if(rows==null||rows.Length>100)throw new FormatException("Resposta inválida do Anitsu.");
   var result=new AnitsuSearchResult();foreach(var row in rows){var item=row as Dictionary<string,object>;if(item==null)throw new FormatException("Resultado inválido.");
    var candidate=new AnitsuCandidate{Name=StringValue(item,"name"),Path=StringValue(item,"path"),Kind=StringValue(item,"kind")};
    if(!ValidCandidate(candidate))throw new FormatException("Caminho inválido do Anitsu.");result.Candidates.Add(candidate);
   }
   result.Candidates=result.Candidates.GroupBy(c=>c.Path,StringComparer.Ordinal).Select(g=>g.First()).ToList();result.State=result.Candidates.Count>0?AnitsuSearchState.Found:AnitsuSearchState.NotFound;return result;
  }catch(FormatException){throw;}catch(Exception e){throw new FormatException("Não foi possível interpretar a resposta do Anitsu.",e);}
 }
 public static string StringValue(Dictionary<string,object> item,string key){object value;return item.TryGetValue(key,out value)&&value is string?(string)value:"";}
 public static bool IsSite(string url){Uri uri;return Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&(uri.Host=="anitsu.moe"||uri.Host=="nuvem.anitsu.moe")&&String.IsNullOrEmpty(uri.UserInfo)&&uri.IsDefaultPort;}
}
}
