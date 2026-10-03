using System;
using System.IO;
using System.Text;
using AniLista;
class AnitsuEmbeddedTests {
 static int count;static void Assert(bool value,string message){if(!value)throw new Exception(message);count++;}
 static int Main(){string folder=Path.Combine(Path.GetTempPath(),"AniLista-downloader-"+Guid.NewGuid().ToString("N"));try{
  Assert(AnitsuDownloadPolicy.IsDownload("https://nuvem.anitsu.moe/api/download?path=Anime%2FLain.mkv"),"Cloud download accepted");
  Assert(!AnitsuDownloadPolicy.IsDownload("https://nuvem.anitsu.moe.evil.example/api/download?path=x"),"Lookalike rejected");
  Assert(!AnitsuDownloadPolicy.IsDownload("file:///C:/Windows/test"),"Local path URL rejected");
  Assert(!AnitsuDownloadPolicy.IsDownload("https://nuvem.anitsu.moe/api/download?path=..%2Fprivate"),"Traversal rejected");
  Assert(!AnitsuDownloadPolicy.IsDownload("https://nuvem.anitsu.moe/api/download?path="+new string('x',4100)),"Excessive URL rejected");
  Assert(AnitsuDownloadPolicy.IsRequest("https://graphql.anilist.co/","POST"),"Covers GraphQL allowed");
  Assert(AnitsuDownloadPolicy.IsRequest("http://127.0.0.1:15151/ping","POST"),"ABDM ping allowed");
  Assert(!AnitsuDownloadPolicy.IsRequest("http://127.0.0.1:15151/delete","POST"),"Unexpected local operation rejected");
  Assert(!AnitsuDownloadPolicy.IsRequest("https://evil.example/","POST"),"External request rejected");
  Assert(AnitsuDownloadPolicy.SafeName("../CON.mkv").IndexOf('/')<0,"Filename cannot escape destination");
  Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"Lain.mkv"),"keep");string partial=AnitsuDownloadPolicy.Reserve(folder,"Lain.mkv");File.WriteAllText(partial,"download");string installed=AnitsuDownloadPolicy.Complete(partial,folder,"Lain.mkv");
  Assert(File.ReadAllText(Path.Combine(folder,"Lain.mkv"))=="keep"&&File.ReadAllText(installed)=="download","Existing download is not overwritten");
  Assert(Path.GetDirectoryName(installed)==folder&&!File.Exists(partial),"Download stays in destination and temporary file removed");
  Assert(EmbeddedAnitsuAssets.Downloader.IndexOf("@version      1.6.8",StringComparison.Ordinal)>=0,"Correct release bundled");
  using(var resource=typeof(EmbeddedAnitsuAssets).Assembly.GetManifestResourceStream("AniLista.Anitsu.Anitsu-Downloader.user.js"))using(var sha=System.Security.Cryptography.SHA256.Create())Assert(BitConverter.ToString(sha.ComputeHash(resource)).Replace("-","").ToLowerInvariant()=="b160100e1149777274a3ca5eceb824876a8de98f545b32b17dcf431eb374b4d6","Exact release bytes embedded");
  Console.WriteLine("PASS: Anitsu integrado "+count+" assertions");return 0;
 }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}}
}
