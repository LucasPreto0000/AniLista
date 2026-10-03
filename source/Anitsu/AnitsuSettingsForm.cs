using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace AniLista {
public sealed class AnitsuSettingsForm:DpiForm {
 readonly ComboBox modes;readonly Label state;readonly AnitsuWebViewProvider web;readonly AnitsuBridgeServer bridge;readonly string folder;readonly Action<AnitsuMode> apply;
 public AnitsuSettingsForm(AnitsuMode selected,string dataFolder,AnitsuWebViewProvider login,AnitsuBridgeServer extension,Action<AnitsuMode> save){
  folder=dataFolder;web=login;bridge=extension;apply=save;Text="Anitsu · busca automática";BackColor=Theme.Background;ForeColor=Theme.Text;ClientSize=Theme.S(this,690,420);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;Theme.DarkTitle(this);
  var title=Theme.Label(this,"Buscar no Anitsu ao adicionar um anime",16,Theme.Text,FontStyle.Bold);Theme.Place(this,title,22,18,646,38);Controls.Add(title);
  modes=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Input,ForeColor=Theme.Text,Font=Theme.Font(this,11)};modes.Items.AddRange(new object[]{"Desativado","Login dentro do AniLista","Extensão Chrome / Edge (usa seu login atual)"});modes.SelectedIndex=(int)selected;Theme.Place(this,modes,22,66,646,36);Controls.Add(modes);
  var description=Theme.Label(this,"Login no app: pesquise com uma sessão própria. O navegador abre o Cloud e o caminho aparece no AniLista.\n\nExtensão: usa a sessão do Chrome/Edge e abre a pasta encontrada. Carregue a pasta da extensão em chrome://extensions ou edge://extensions, com Modo do desenvolvedor ligado.",10,Theme.Muted);Theme.Place(this,description,22,118,646,130);Controls.Add(description);
  state=Theme.Label(this,"",10,Theme.Text);Theme.Place(this,state,22,255,646,60);Controls.Add(state);
  var saveButton=Theme.Button(this,"Salvar modo",true);Theme.Place(this,saveButton,22,330,145,42);saveButton.Click+=delegate{Run(delegate{apply((AnitsuMode)modes.SelectedIndex);RefreshState();return Task.FromResult(0);});};Controls.Add(saveButton);
  var connect=Theme.Button(this,"Conectar / reparar");Theme.Place(this,connect,178,330,176,42);connect.Click+=delegate{Run(async delegate{
   var mode=(AnitsuMode)modes.SelectedIndex;apply(mode);if(mode==AnitsuMode.AppLogin)await web.Connect(this);else if(mode==AnitsuMode.BrowserExtension){AnitsuExtensionRegistration.Register(folder,Application.ExecutablePath);bridge.Start();Process.Start(new ProcessStartInfo(ExtensionFolder()){UseShellExecute=true});state.Text="Registro pronto. Carregue esta pasta no navegador e clique no ícone da extensão.";}else RefreshState();
  });};Controls.Add(connect);
  var disconnect=Theme.Button(this,"Desconectar");Theme.Place(this,disconnect,365,330,143,42);disconnect.Click+=delegate{Run(async delegate{apply(AnitsuMode.Disabled);modes.SelectedIndex=0;AnitsuExtensionRegistration.Unregister();RefreshState();await web.Disconnect();});};Controls.Add(disconnect);
  var close=Theme.Button(this,"Fechar");Theme.Place(this,close,520,330,148,42);close.Click+=delegate{Close();};Controls.Add(close);
  var timer=new System.Windows.Forms.Timer{Interval=1500};timer.Tick+=delegate{if((AnitsuMode)modes.SelectedIndex==AnitsuMode.BrowserExtension&&bridge.Connected)state.Text="Extensão conectada. Novos animes salvos serão pesquisados.";};timer.Start();FormClosed+=delegate{timer.Dispose();};RefreshState();
 }
 string ExtensionFolder(){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"extensions","anitsu");if(!Directory.Exists(path))path=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","extensions","anitsu"));if(!Directory.Exists(path))throw new DirectoryNotFoundException("Pasta extensions\\anitsu não encontrada junto ao projeto.");return path;}
 async void Run(Func<Task> action){Enabled=false;try{await action();}catch(Exception e){state.Text=e.Message;}finally{if(!IsDisposed)Enabled=true;}}
 void RefreshState(){var mode=(AnitsuMode)modes.SelectedIndex;state.Text=mode==AnitsuMode.Disabled?"A biblioteca continua salva normalmente, com a busca desativada.":mode==AnitsuMode.AppLogin?"Use Conectar para entrar no site verdadeiro do Anitsu.":bridge.Connected?"Extensão conectada.":AnitsuExtensionRegistration.IsRegistered(folder,Application.ExecutablePath)?"Abra o AniLista e clique no ícone da extensão no navegador para conectar.":"Use Conectar / reparar para registrar este executável e abrir a pasta da extensão.";}
}
}
