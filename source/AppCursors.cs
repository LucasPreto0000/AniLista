using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AniLista {
// Windows 11 Cursors Concept, por jepriCreations (mesmo pacote do YT-DLP Deck).
public static class AppCursors {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadCursorFromFile(string file);
 public static readonly Cursor Arrow=Load("arrow.cur",Cursors.Default);
 public static readonly Cursor Hand=Load("hand.cur",Cursors.Hand);
 public static readonly Cursor Text=Load("ibeam.cur",Cursors.IBeam);
 static Cursor Load(string name,Cursor fallback){
  try{
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AniLista.Cursors."+name)){
    if(stream==null)return fallback;
    string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AniLista","cursores");Directory.CreateDirectory(folder);
    string file=Path.Combine(folder,name);using(var output=File.Create(file))stream.CopyTo(output);
    IntPtr handle=LoadCursorFromFile(file);return handle==IntPtr.Zero?fallback:new Cursor(handle);
   }
  }catch{return fallback;}
 }
 public static void Apply(Control control){
  if(control is TextBoxBase||control.Cursor==Cursors.IBeam)control.Cursor=Text;
  else if(control.Cursor==Cursors.Hand)control.Cursor=Hand;
  else if(control.Cursor==Cursors.Default||control.Cursor==Cursors.Arrow)control.Cursor=Arrow;
  foreach(Control child in control.Controls)Apply(child);
  control.ControlAdded+=delegate(object sender,ControlEventArgs args){Apply(args.Control);};
 }
}
}
