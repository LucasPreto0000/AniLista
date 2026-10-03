using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
namespace AniLista {
public sealed class AnitsuSettingsStore {
 public readonly string Folder,FilePath;
 public AnitsuSettingsStore(string folder){Folder=folder;FilePath=Path.Combine(folder,"anitsu-settings.json");}
 public AnitsuMode Load(){try{if(!File.Exists(FilePath)||new FileInfo(FilePath).Length>4096)return AnitsuMode.Disabled;var item=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath));return item!=null&&Enum.IsDefined(typeof(AnitsuMode),item.Mode)?(AnitsuMode)item.Mode:AnitsuMode.Disabled;}catch{return AnitsuMode.Disabled;}}
 public void Save(AnitsuMode mode){if(!Enum.IsDefined(typeof(AnitsuMode),mode))throw new ArgumentException("Modo inválido.");Directory.CreateDirectory(Folder);string temp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,new JavaScriptSerializer().Serialize(new Settings{Mode=(int)mode}),Encoding.UTF8);if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public sealed class Settings {public int Mode;}
}
}
