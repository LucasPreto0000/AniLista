using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
namespace AniLista {
public static class AnitsuNativeProtocol {
 public static string PipeName{get{return "AniLista-Anitsu-"+WindowsIdentity.GetCurrent().User.Value;}}
 public static string Read(Stream stream){byte[] header=new byte[4];int first=stream.ReadByte();if(first<0)return null;header[0]=(byte)first;ReadExact(stream,header,1,3);uint size=BitConverter.ToUInt32(header,0);if(size==0||size>AnitsuApi.MaxBytes)throw new FormatException("Mensagem grande demais.");byte[] body=new byte[size];ReadExact(stream,body,0,body.Length);return new UTF8Encoding(false,true).GetString(body);}
 static void ReadExact(Stream stream,byte[] bytes,int start,int count){while(count>0){int got=stream.Read(bytes,start,count);if(got==0)throw new EndOfStreamException();start+=got;count-=got;}}
 public static void Write(Stream stream,string json){byte[] bytes=Encoding.UTF8.GetBytes(json);if(bytes.Length==0||bytes.Length>AnitsuApi.MaxBytes)throw new FormatException("Mensagem grande demais.");byte[] header=BitConverter.GetBytes((uint)bytes.Length);stream.Write(header,0,4);stream.Write(bytes,0,bytes.Length);stream.Flush();}
 public static Dictionary<string,object> Parse(string text){if(text==null||Encoding.UTF8.GetByteCount(text)>AnitsuApi.MaxBytes)throw new FormatException("Mensagem inválida.");var row=new JavaScriptSerializer{MaxJsonLength=AnitsuApi.MaxBytes,RecursionLimit=12}.DeserializeObject(text) as Dictionary<string,object>;if(row==null)throw new FormatException("Mensagem inválida.");return row;}
 public static string Json(object message){return new JavaScriptSerializer{MaxJsonLength=AnitsuApi.MaxBytes}.Serialize(message);}
}
}
