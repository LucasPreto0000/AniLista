using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AniLista {
// Escala manual para preservar a distribuição de um único EXE no .NET Framework.
public class DpiForm:Form {
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
 [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
 [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
 readonly List<Font> scaledFonts=new List<Font>();
 public float LayoutScale {get;private set;}
 public event EventHandler LayoutScaleChanged;
 public DpiForm(){LayoutScale=1f;AutoScaleMode=AutoScaleMode.None;AppCursors.Apply(this);}
 public static void EnablePerMonitor(){try{SetProcessDpiAwarenessContext(new IntPtr(-4));}catch(EntryPointNotFoundException){}catch(DllNotFoundException){}}
 public static float HandleScale(IntPtr window){try{uint dpi=GetDpiForWindow(window);return dpi>0?dpi/96f:1f;}catch{return 1f;}}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ApplyScale(HandleScale(Handle));}
 protected override void WndProc(ref Message m){
  if(m.Msg==0x02E0){
   float next=(m.WParam.ToInt32()&0xffff)/96f;
   var suggested=(NativeRect)Marshal.PtrToStructure(m.LParam,typeof(NativeRect));
   ApplyScale(next);Bounds=new Rectangle(suggested.Left,suggested.Top,suggested.Right-suggested.Left,suggested.Bottom-suggested.Top);
   m.Result=IntPtr.Zero;return;
  }
  base.WndProc(ref m);
 }
 public void ApplyScale(float next){
  if(next<=0||Math.Abs(next-LayoutScale)<0.001f)return;
  float ratio=next/LayoutScale;
  var oldFonts=new Dictionary<Control,Font>();CaptureFonts(this,oldFonts);
  Font[] previousFonts=scaledFonts.ToArray();scaledFonts.Clear();
  SuspendLayout();LayoutScale=next;
  try{
   Size oldMinimum=MinimumSize;MinimumSize=Size.Empty;
   Scale(new SizeF(ratio,ratio));
   MinimumSize=new Size((int)Math.Round(oldMinimum.Width*ratio),(int)Math.Round(oldMinimum.Height*ratio));
   foreach(var pair in oldFonts){
    var font=new Font(pair.Value.FontFamily,pair.Value.Size*ratio,pair.Value.Style,pair.Value.Unit);scaledFonts.Add(font);pair.Key.Font=font;
   }
   if(LayoutScaleChanged!=null)LayoutScaleChanged(this,EventArgs.Empty);
  }finally{ResumeLayout(true);Invalidate(true);foreach(Font f in previousFonts)f.Dispose();}
 }
 static void CaptureFonts(Control control,Dictionary<Control,Font> fonts){fonts[control]=control.Font;foreach(Control child in control.Controls)CaptureFonts(child,fonts);}
 protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing){foreach(Font f in scaledFonts)f.Dispose();scaledFonts.Clear();}}
}
}
