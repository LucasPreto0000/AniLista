using System;
using System.IO;
using System.Text;
namespace AniLista {
public static class EmbeddedAnitsuAssets {
 public static string Read(string file){using(var stream=typeof(EmbeddedAnitsuAssets).Assembly.GetManifestResourceStream("AniLista.Anitsu."+file)){if(stream==null)throw new FileNotFoundException("Recurso Anitsu ausente: "+file);using(var reader=new StreamReader(stream,Encoding.UTF8))return reader.ReadToEnd();}}
 public static string Downloader{get{return Read("Anitsu-Downloader.user.js");}}
 public static string Bootstrap{get{return Read("compat.js")+"\n(function(){if(location.origin!=='https://nuvem.anitsu.moe'||window!==window.top)return;function start(){if(window.__aniListaDownloaderLoaded)return;window.__aniListaDownloaderLoaded=true;"+Downloader+"\nwindow.aniListaEmbeddedReady();}if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();})();";}}
}
}
