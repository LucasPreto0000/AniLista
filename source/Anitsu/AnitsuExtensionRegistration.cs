using System;
using System.IO;
using Microsoft.Win32;
namespace AniLista {
public static class AnitsuExtensionRegistration {
 static readonly string[] Keys={"Software\\Google\\Chrome\\NativeMessagingHosts\\"+ExtensionIdentity.Host,"Software\\Microsoft\\Edge\\NativeMessagingHosts\\"+ExtensionIdentity.Host};
 public static void Register(string folder,string executable){
  if(!File.Exists(executable))throw new FileNotFoundException("Executável ausente.");Directory.CreateDirectory(folder);string manifest=Path.Combine(folder,"anitsu-native-host.json");
  File.WriteAllText(manifest,AnitsuNativeProtocol.Json(new{name=ExtensionIdentity.Host,description="AniLista Anitsu",path=Path.GetFullPath(executable),type="stdio",allowed_origins=new[]{ExtensionIdentity.Origin}}),new System.Text.UTF8Encoding(false));
  foreach(string path in Keys)using(var key=Registry.CurrentUser.CreateSubKey(path))key.SetValue("",manifest,RegistryValueKind.String);
 }
 public static void Unregister(){foreach(string path in Keys)Registry.CurrentUser.DeleteSubKeyTree(path,false);}
 public static bool IsRegistered(string folder,string executable){try{string file=Path.Combine(folder,"anitsu-native-host.json");var row=AnitsuNativeProtocol.Parse(File.ReadAllText(file));if(!String.Equals(AnitsuApi.StringValue(row,"path"),Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase))return false;foreach(string path in Keys)using(var key=Registry.CurrentUser.OpenSubKey(path))if(key==null||!String.Equals(key.GetValue("") as string,file,StringComparison.OrdinalIgnoreCase))return false;return true;}catch{return false;}}
}
}
