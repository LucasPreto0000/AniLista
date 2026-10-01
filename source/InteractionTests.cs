using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AniLista;
class InteractionTests {
 static IEnumerable<Control> Walk(Control root) { foreach(Control c in root.Controls) { yield return c; foreach(Control child in Walk(c)) yield return child; } }
 static void Assert(bool success,string name) { if(!success)throw new Exception(name); }
 static Button Button(Form form,string title) {return Walk(form).OfType<Button>().First(b=>b.Text==title||b.Text.StartsWith(title+"   "));}
 [STAThread] static int Main(){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string folder=Path.Combine(Path.GetTempPath(),"AniLista-interactions-"+Guid.NewGuid().ToString("N"));
  var store=new LibraryStore(folder);
  var watching=new Anime{Title="Teste episódio",Status="watching",Episode=1,Total=2};
  var planned=new Anime{Title="Teste planejado",Status="planned",Episode=0,Total=12};
  var completed=new Anime{Title="Teste concluído",Status="completed",Episode=12,Total=12};
  store.Save(new List<Anime>{watching,planned,completed});
  try{
   using(var main=new MainForm(store,store.Load())){
    main.ShowInTaskbar=false;main.Opacity=0;main.Show();Application.DoEvents();
    var card=Walk(main).OfType<LinePanel>().First();
    Assert(card.Controls.OfType<Label>().All(l=>l.Right<=card.Width&&l.Left>=0),"Texto cabe dentro do cartão");
    Button(main,"+1 episódio").PerformClick();Application.DoEvents();
    Assert(store.Load().Single(a=>a.Id==watching.Id).Status=="completed","Último episódio move para concluídos");
    Button(main,"Concluídos").PerformClick();Application.DoEvents();
    Assert(Walk(main).OfType<LinePanel>().Count()==2,"Dois cartões concluídos");
    Button(main,"Voltar a assistir").PerformClick();Application.DoEvents();
    Assert(store.Load().Single(a=>a.Id==watching.Id).Status=="watching"&&store.Load().Single(a=>a.Id==watching.Id).Episode==0,"Voltar a assistir reinicia progresso");
    Button(main,"Quero assistir").PerformClick();Application.DoEvents();
    Button(main,"Começar a assistir").PerformClick();Application.DoEvents();
    Assert(store.Load().Single(a=>a.Id==planned.Id).Status=="watching","Planejado move para assistindo");
    main.Close();
   }
   using(var editor=new EditorForm(new Anime(),true,"watching",delegate(Anime a){var list=store.Load();list.Add(a);store.Save(list);return true;})){
    editor.ShowInTaskbar=false;editor.Opacity=0;editor.Show();Application.DoEvents();
    Walk(editor).OfType<TextBox>().First().Text="Teste cadastro manual";
    var numbers=Walk(editor).OfType<Stepper>().OrderBy(n=>n.Left).ToArray();numbers[0].Value=5;numbers[1].Value=24;
    Button(editor,"Adicionar anime").PerformClick();Application.DoEvents();
    Assert(store.Load().Any(a=>a.Title=="Teste cadastro manual"&&a.Episode==5&&a.Total==24&&a.Status=="watching"),"Cadastro manual salva episódio e total");
   }
   Console.WriteLine("PASS: cartões, incremento, conclusão, três listas, reinício e cadastro manual.");return 0;
  }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
 }
}
