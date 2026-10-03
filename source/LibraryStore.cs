using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace AniLista {
public sealed class LibraryStore {
 public const int BackupCount=1;
 public readonly string Folder,FilePath;
 string loadedSignature;bool hasBaseline;
 public bool Recovered { get; private set; }
 public string BackupWarning { get; private set; }
 public LibraryStore(string folder){Folder=Path.GetFullPath(folder);FilePath=Path.Combine(Folder,"biblioteca.json");}
 public static string DefaultFolder{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AniLista");}}
 public sealed class BackupInfo {
  public string Path;public DateTime Saved;public int Count;
  public override string ToString(){return Saved.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")+" · "+Count+(Count==1?" anime":" animes");}
 }
 static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
 string Signature(){return File.Exists(FilePath)?Hash(File.ReadAllBytes(FilePath)):null;}
 T WithLock<T>(Func<T> operation){
  string name="Local\\AniLista-Store-"+Hash(Encoding.UTF8.GetBytes(FilePath.ToUpperInvariant()));
  using(var mutex=new Mutex(false,name)){
   bool acquired=false;
   try{try{acquired=mutex.WaitOne(10000);}catch(AbandonedMutexException){acquired=true;}if(!acquired)throw new IOException("Outra gravação está em andamento. Tente novamente.");return operation();}
   finally{if(acquired)mutex.ReleaseMutex();}
  }
 }
 public string BackupPath(int index){if(index!=0)throw new ArgumentOutOfRangeException("index");return FilePath+".bak";}
 IEnumerable<string> LegacyBackups(){return Enumerable.Range(1,4).Select(i=>FilePath+".bak."+i).Where(File.Exists);}
 Library Read(string path){
  using(var stream=File.OpenRead(path)){
   var library=(Library)new DataContractJsonSerializer(typeof(Library)).ReadObject(stream);
   if(library==null||library.Version!=1||library.Animes==null)throw new IOException("Formato da biblioteca inválido.");
   var ids=new HashSet<string>(StringComparer.Ordinal);
   foreach(var a in library.Animes){
    if(a==null||String.IsNullOrWhiteSpace(a.Id)||!ids.Add(a.Id))throw new IOException("Uma anotação está inválida.");
    // Validar uma cópia evita alterar a data de atualização durante a leitura.
    try{a.Copy().Validate();}catch(Exception e){throw new IOException("Uma anotação está inválida.",e);}
   }
   return library;
  }
 }
 bool Valid(string path){try{Read(path);return true;}catch{return false;}}
 IEnumerable<string> RecoveryFiles(){return new[]{BackupPath(0)}.Where(File.Exists).Concat(LegacyBackups());}
 public List<BackupInfo> GetBackups(){return WithLock(()=>new[]{BackupPath(0)}.Where(File.Exists).Select(path=>{try{return new BackupInfo{Path=path,Saved=File.GetLastWriteTimeUtc(path),Count=Read(path).Animes.Count};}catch{return null;}}).Where(info=>info!=null).ToList());}
 public List<Anime> ReadBackup(string path){return Read(path).Animes.Select(a=>a.Copy()).ToList();}
 public static List<Anime> MergeMissing(List<Anime> current,List<Anime> backup){
  var merged=current.Select(a=>a.Copy()).ToList();
  foreach(var anime in backup){if(merged.Any(a=>a.Id==anime.Id||(a.CatalogId>0&&a.CatalogId==anime.CatalogId)||String.Equals(a.Title.Trim(),anime.Title.Trim(),StringComparison.CurrentCultureIgnoreCase)))continue;merged.Add(anime.Copy());}
  return merged;
 }
 public void Export(string path){WithLock(()=>{
  string target=Path.GetFullPath(path);
  if(target.Equals(FilePath,StringComparison.OrdinalIgnoreCase)||(Path.GetDirectoryName(target).Equals(Folder,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(target).StartsWith("biblioteca.json",StringComparison.OrdinalIgnoreCase)))throw new IOException("Escolha outro arquivo para exportar a cópia.");
  if(!File.Exists(FilePath)&&RecoveryFiles().Any())throw new IOException("O arquivo principal está ausente. Reabra o AniLista para recuperar a biblioteca antes de exportar.");
  Library library=File.Exists(FilePath)?Read(FilePath):new Library();string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){new DataContractJsonSerializer(typeof(Library)).WriteObject(stream,library);stream.Flush(true);}Read(temporary);if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);}
  finally{if(File.Exists(temporary))File.Delete(temporary);}return 0;
 });}
 public List<Anime> Load(){return WithLock(()=>{var items=LoadCore();loadedSignature=Signature();hasBaseline=true;return items;});}
 List<Anime> LoadCore(){
  Directory.CreateDirectory(Folder);
  Recovered=false;
  if(File.Exists(FilePath)){
   Library current=null;try{current=Read(FilePath);}catch{}
    if(current!=null){BackupWarning=null;try{if(!File.Exists(BackupPath(0))||!Valid(BackupPath(0))||Hash(File.ReadAllBytes(BackupPath(0)))!=Signature()||LegacyBackups().Any())WriteBackup(FilePath);RemoveLegacyBackups();}catch(IOException e){BackupWarning="Biblioteca carregada; o backup não pôde ser atualizado: "+e.Message;}catch(UnauthorizedAccessException e){BackupWarning="Biblioteca carregada; o backup não pôde ser atualizado: "+e.Message;}return current.Animes;}
  }
  var candidates=RecoveryFiles().ToList();
  if(!File.Exists(FilePath)&&candidates.Count==0)return new List<Anime>();
  foreach(string path in candidates){
   Library library;
   try{library=Read(path);}catch{continue;}
   // O backup validado é mantido ao recuperar um arquivo principal inválido.
   Install(library.Animes,false);
   RemoveLegacyBackups();
   Recovered=true;
   return library.Animes;
  }
  throw new IOException("Nenhuma cópia válida foi encontrada. Os arquivos foram preservados em "+Folder);
 }
 void WriteBackup(string path){
  Read(path);string destination=BackupPath(0),temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{using(var input=File.OpenRead(path))using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){input.CopyTo(output);output.Flush(true);}Read(temporary);if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);}
  finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 void RemoveLegacyBackups(){if(Valid(BackupPath(0)))foreach(string path in LegacyBackups())File.Delete(path);}
 public void Save(List<Anime> items){
  if(items==null)throw new ArgumentNullException("items");
  WithLock(()=>{
   string current=Signature();
   if((hasBaseline&&current!=loadedSignature)||(!hasBaseline&&(current!=null||RecoveryFiles().Any())))throw new IOException("A biblioteca no disco mudou desde que o aplicativo abriu. Reabra o AniLista antes de salvar para não sobrescrever seus animes.");
   Install(items.Select(a=>a==null?null:a.Copy()).ToList(),true);loadedSignature=Signature();hasBaseline=true;return 0;
  });
 }
 void Install(List<Anime> items,bool backup){
  Directory.CreateDirectory(Folder);
  string temporary=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{
   using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
    new DataContractJsonSerializer(typeof(Library)).WriteObject(stream,new Library{Animes=items});stream.Flush(true);
   }
   Read(temporary);
   bool validCurrent=File.Exists(FilePath)&&Valid(FilePath);
   if(backup&&File.Exists(FilePath)&&!validCurrent)throw new IOException("A biblioteca no disco mudou ou está corrompida. Reabra o aplicativo para recuperar o backup antes de salvar.");
   BackupWarning=null;
   if(!backup)WriteBackup(temporary);
   if(File.Exists(FilePath)){
    // A troca preserva o principal anterior no único .bak; falhas não adiantam o backup.
    File.Replace(temporary,FilePath,backup?BackupPath(0):null);
    if(backup)try{WriteBackup(FilePath);}catch(IOException e){BackupWarning="Biblioteca salva; o backup não pôde ser atualizado: "+e.Message;}catch(UnauthorizedAccessException e){BackupWarning="Biblioteca salva; o backup não pôde ser atualizado: "+e.Message;}
   }else{
    if(backup)WriteBackup(temporary);
    try{File.Move(temporary,FilePath);}catch{if(backup&&File.Exists(BackupPath(0)))File.Delete(BackupPath(0));throw;}
   }
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}
}
