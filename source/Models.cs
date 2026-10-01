using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace AniLista {
[DataContract] public sealed class Anime {
 [DataMember] public string Id=Guid.NewGuid().ToString("N");
 [DataMember] public int CatalogId;
 [DataMember] public string Title="", Cover="", Status="planned";
 [DataMember] public int Episode, Total, Year;
 [DataMember] public string Updated=DateTime.UtcNow.ToString("o");
 public Anime Copy(){return (Anime)MemberwiseClone();}
 public void Validate(){
  Title=(Title??"").Trim();
  if(Title.Length==0||Title.Length>180)throw new Exception("Informe um título com até 180 caracteres.");
  if(Status!="planned"&&Status!="watching"&&Status!="completed")throw new Exception("Selecione uma lista válida.");
  if(Episode<0||Total<0||Total>100000||Episode>100000)throw new Exception("Confira os números de episódios.");
  if(Status=="planned")Episode=0;
  if(Status=="completed"&&Total>0)Episode=Total;
  if(Total>0&&Episode>Total)throw new Exception("O episódio atual não pode ser maior que o total.");
  if(String.IsNullOrEmpty(Id))Id=Guid.NewGuid().ToString("N");
  Updated=DateTime.UtcNow.ToString("o");
 }
}
[DataContract] public sealed class Library {
 [DataMember] public int Version=1;
 [DataMember] public List<Anime> Animes=new List<Anime>();
}
}
