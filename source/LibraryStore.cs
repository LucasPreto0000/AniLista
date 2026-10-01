using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace AniLista {
public sealed class LibraryStore {
 public const int BackupCount=5;
 public readonly string Folder,FilePath;
 public bool Recovered { get; private set; }
 public LibraryStore(string folder){Folder=folder;FilePath=Path.Combine(folder,"biblioteca.json");}
 public string BackupPath(int index){return FilePath+".bak"+(index==0?"":"."+index);}
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
 public List<Anime> Load(){
  Directory.CreateDirectory(Folder);
  Recovered=false;
  if(File.Exists(FilePath)){
   try{return Read(FilePath).Animes;}catch{}
  }else if(!Enumerable.Range(0,BackupCount).Any(i=>File.Exists(BackupPath(i))))return new List<Anime>();
  for(int i=0;i<BackupCount;i++){
   Library library;
   try{library=Read(BackupPath(i));}catch{continue;}
   // Restaurar imediatamente, sem fazer o arquivo corrompido ocupar um backup válido.
   if(File.Exists(FilePath))File.Copy(FilePath,FilePath+".corrompido-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N"),false);
   Install(library.Animes,false);
   Recovered=true;
   return library.Animes;
  }
  throw new IOException("Nenhuma cópia válida foi encontrada. Os arquivos foram preservados em "+Folder);
 }
 void RotateBackups(){
  // Só cópias verificadas entram na rotação; lacunas e backups inválidos são ignorados.
  var valid=Enumerable.Range(0,BackupCount).Select(BackupPath).Where(p=>File.Exists(p)&&Valid(p)).Take(BackupCount-1).ToList();
  var staged=new List<string>();
  try{
   foreach(string path in valid){string temp=FilePath+"."+Guid.NewGuid().ToString("N")+".rotate";File.Copy(path,temp,false);staged.Add(temp);}
   for(int i=0;i<staged.Count;i++)File.Copy(staged[i],BackupPath(i+1),true);
  }finally{foreach(string p in staged)if(File.Exists(p))File.Delete(p);}
 }
 public void Save(List<Anime> items){
  if(items==null)throw new ArgumentNullException("items");
  Install(items,true);
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
   if(backup&&validCurrent)RotateBackups();
   if(File.Exists(FilePath))File.Replace(temporary,FilePath,backup&&validCurrent?BackupPath(0):null);
   else File.Move(temporary,FilePath);
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}
}
