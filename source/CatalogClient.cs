using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AniLista {
public sealed class SearchResult {
 public Anime Anime; public string Alternative="",Format="",Thumb=""; public Image Image;
 public SearchResult Copy(){return new SearchResult{Anime=Anime.Copy(),Alternative=Alternative,Format=Format,Thumb=Thumb};}
 public override string ToString(){return Anime.Title;}
}
public sealed class SearchPage {
 public readonly List<SearchResult> Results;
 public readonly int Page;
 public readonly bool HasNextPage;
 public SearchPage(List<SearchResult> results,int page,bool hasNext){Results=results;Page=page;HasNextPage=hasNext;}
 public SearchPage Copy(){return new SearchPage(Results.Select(r=>r.Copy()).ToList(),Page,HasNextPage);}
}
public sealed class CatalogException:Exception {public CatalogException(string message):base(message){}}
public interface ICatalogClient {
 Task<SearchPage> SearchAsync(string text,int page,CancellationToken token,Action<int> waiting);
}
public sealed class CatalogClient:ICatalogClient {
 sealed class Cached {public SearchPage Page;public DateTime Expires;}
 readonly HttpClient client;
 readonly SemaphoreSlim requests=new SemaphoreSlim(1,1);
 readonly Dictionary<string,Cached> cache=new Dictionary<string,Cached>(StringComparer.Ordinal);
 readonly Func<TimeSpan,CancellationToken,Task> delay;
 DateTime retryUntil=DateTime.MinValue,nextQuery=DateTime.MinValue;
 public CatalogClient():this(new HttpClient{Timeout=TimeSpan.FromSeconds(18)},null){}
 public CatalogClient(HttpClient http,Func<TimeSpan,CancellationToken,Task> wait){client=http;delay=wait??((time,token)=>Task.Delay(time,token));}
 static Dictionary<string,object> Obj(object value){return value as Dictionary<string,object>;}
 static object Get(Dictionary<string,object> d,string key){object v;return d!=null&&d.TryGetValue(key,out v)?v:null;}
 static int Int(object o){try{return o==null?0:Convert.ToInt32(o,CultureInfo.InvariantCulture);}catch{return 0;}}
 static string Str(object o){return o==null?"":Convert.ToString(o,CultureInfo.InvariantCulture);}
 static string FormatName(string f){switch(f){case "TV":return "Série de TV";case "TV_SHORT":return "TV curta";case "MOVIE":return "Filme";case "SPECIAL":return "Especial";case "OVA":return "OVA";case "ONA":return "ONA";case "MUSIC":return "Clipe musical";default:return "";}}
 static int RetrySeconds(HttpResponseMessage response){
  if(response.Headers.RetryAfter!=null){
   if(response.Headers.RetryAfter.Delta.HasValue)return Math.Max(1,(int)Math.Ceiling(response.Headers.RetryAfter.Delta.Value.TotalSeconds));
   if(response.Headers.RetryAfter.Date.HasValue)return Math.Max(1,(int)Math.Ceiling((response.Headers.RetryAfter.Date.Value-DateTimeOffset.UtcNow).TotalSeconds));
  }
  IEnumerable<string> reset;long seconds;
  if(response.Headers.TryGetValues("X-RateLimit-Reset",out reset)&&long.TryParse(reset.FirstOrDefault(),out seconds))return Math.Max(1,(int)Math.Min(3600,seconds-(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds));
  return 60;
 }
 async Task WaitAsync(int seconds,CancellationToken token,Action<int> waiting){
  // Intervalos curtos mantêm a contagem visível e permitem cancelar ao trocar a busca.
  for(int remaining=seconds;remaining>0;remaining--){token.ThrowIfCancellationRequested();if(waiting!=null)waiting(remaining);await delay(TimeSpan.FromSeconds(1),token);}
 }
 public async Task<SearchPage> SearchAsync(string text,int page,CancellationToken token,Action<int> waiting){
  text=(text??"").Trim();if(text.Length<2||page<1)throw new ArgumentException("Busca ou página inválida.");
  string key=text.ToUpperInvariant()+"|"+page;
  await requests.WaitAsync(token);
  try{
   token.ThrowIfCancellationRequested();Cached hit;
   if(cache.TryGetValue(key,out hit)&&hit.Expires>DateTime.UtcNow)return hit.Page.Copy();
   TimeSpan spacing=nextQuery-DateTime.UtcNow;if(spacing>TimeSpan.Zero)await delay(spacing,token);
   int initialWait=(int)Math.Ceiling((retryUntil-DateTime.UtcNow).TotalSeconds);
   if(initialWait>0)await WaitAsync(initialWait,token,waiting);
   var json=new JavaScriptSerializer{MaxJsonLength=4000000};
   var payload=new {query="query($search:String,$page:Int){Page(page:$page,perPage:20){pageInfo{hasNextPage} media(search:$search,type:ANIME,sort:POPULARITY_DESC,isAdult:false){id title{romaji english} format episodes seasonYear startDate{year} coverImage{large medium}}}}",variables=new{search=text,page=page}};
   for(int attempt=0;attempt<3;attempt++){
    using(var request=new HttpRequestMessage(HttpMethod.Post,"https://graphql.anilist.co")){
     request.Headers.UserAgent.ParseAdd("AniLista/1.1");request.Headers.Accept.ParseAdd("application/json");request.Content=new StringContent(json.Serialize(payload),Encoding.UTF8,"application/json");
     HttpResponseMessage response;
     try{nextQuery=DateTime.UtcNow.AddSeconds(1);response=await client.SendAsync(request,token);}catch(HttpRequestException){throw new CatalogException("Não foi possível acessar o catálogo. Confira a conexão ou use o cadastro manual.");}
     using(response){
      if((int)response.StatusCode==429){
       int seconds=RetrySeconds(response);retryUntil=DateTime.UtcNow.AddSeconds(seconds);
       if(attempt==2)throw new CatalogException("O catálogo continua limitando as buscas. Aguarde "+seconds+" segundos ou use o cadastro manual.");
       await WaitAsync(seconds,token,waiting);retryUntil=DateTime.MinValue;continue;
      }
      if(!response.IsSuccessStatusCode)throw new CatalogException("O catálogo está indisponível agora (erro "+(int)response.StatusCode+"). Tente novamente ou use o cadastro manual.");
      var root=Obj(json.DeserializeObject(await response.Content.ReadAsStringAsync()));
      var data=Obj(Get(Obj(Get(root,"data")),"Page"));var rows=Get(data,"media") as object[];
      if(rows==null)throw new CatalogException("O catálogo não retornou os resultados. Tente novamente.");
      var results=new List<SearchResult>();
      foreach(object item in rows){
       var row=Obj(item);if(row==null)continue;var titles=Obj(Get(row,"title"));var cover=Obj(Get(row,"coverImage"));
       string romaji=Str(Get(titles,"romaji")).Trim(),english=Str(Get(titles,"english")).Trim(),title=romaji.Length>0?romaji:english;
       if(title.Length==0)continue;if(title.Length>180)title=title.Substring(0,180);
       int year=Int(Get(row,"seasonYear"));if(year<=0)year=Int(Get(Obj(Get(row,"startDate")),"year"));
       results.Add(new SearchResult{Anime=new Anime{CatalogId=Int(Get(row,"id")),Title=title,Total=Math.Max(0,Math.Min(100000,Int(Get(row,"episodes")))),Year=Math.Max(0,year),Cover=Str(Get(cover,"large"))},Alternative=String.Equals(english,title,StringComparison.OrdinalIgnoreCase)?"":english,Format=FormatName(Str(Get(row,"format"))),Thumb=Str(Get(cover,"medium"))});
      }
      bool more=Object.Equals(Get(Obj(Get(data,"pageInfo")),"hasNextPage"),true);
      var result=new SearchPage(results,page,more);
      foreach(string expired in cache.Where(p=>p.Value.Expires<=DateTime.UtcNow).Select(p=>p.Key).ToArray())cache.Remove(expired);
      if(cache.Count>=100)cache.Remove(cache.OrderBy(p=>p.Value.Expires).First().Key);
      cache[key]=new Cached{Page=result.Copy(),Expires=DateTime.UtcNow.AddMinutes(5)};
      return result;
     }
    }
   }
   throw new CatalogException("Não foi possível concluir a busca.");
  }finally{requests.Release();}
 }
}
public static class Catalog {
 public static readonly ICatalogClient Client=new CatalogClient();
 public static string CoverFolder {get{return CoverService.Shared.Folder;}set{CoverService.Shared.Folder=value;}}
 public static async Task<List<SearchResult>> Search(string text,CancellationToken token){return (await Client.SearchAsync(text,1,token,null)).Results;}
 public static Task<Image> LoadImage(string url,CancellationToken token){return CoverService.Shared.LoadImageAsync(url,token);}
 public static async Task LoadCover(System.Windows.Forms.PictureBox picture,Anime anime){
  if(anime==null||anime.CatalogId<=0)return;
  Image image=await CoverService.Shared.LoadCoverAsync(anime,CancellationToken.None);
  if(image==null)return;if(picture.IsDisposed){image.Dispose();return;}
  Image old=picture.Image;picture.Image=image;if(old!=null)old.Dispose();
 }
}
}
