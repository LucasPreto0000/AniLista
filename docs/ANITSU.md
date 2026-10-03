# Buscar animes no Anitsu

Abra **Anitsu** na barra lateral do AniLista. Escolha um modo e clique **Salvar modo**. A integração começa desativada.

## Login dentro do AniLista

Escolha **Login dentro do AniLista**, clique **Conectar / reparar** e faça login no site verdadeiro. Use **Abrir Anitsu Cloud** para verificar a sessão, depois **Concluir login**.

Ao adicionar um anime, a busca usa essa sessão. Se encontrar uma pasta exata e única, abre o Cloud no navegador padrão e mostra o caminho na barra lateral. Este modo abre a página do Cloud; para abrir diretamente dentro da pasta, utilize a extensão.

**Buscar anime salvo** permite tentar novamente depois de fazer login ou recuperar a conexão, sem duplicar o anime. **Copiar caminho** fica disponível assim que uma pasta é escolhida, mesmo se o navegador não conseguir abri-la.

O WebView2 Runtime precisa estar instalado no Windows. O projeto incorpora o SDK ao executável; ele não precisa estar ao lado do código. Se o Runtime estiver ausente, instale-o pelo [site oficial da Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/).

## Usar o login existente no Chrome / Edge

Siga as [instruções da extensão](../extensions/anitsu/README.md). A extensão abre a pasta encontrada usando a interface do site. Mantenha o AniLista aberto e conectado. Nenhum dado de autenticação é enviado ao app.

## Regras e armazenamento

- Apenas novas inclusões salvas disparam a pesquisa. Editar, importar, alterar episódio ou tentar uma duplicata não dispara busca.
- Nomes parecidos ou caminhos diferentes exigem escolher uma pasta. Os números das temporadas são preservados na comparação.
- Trocar de modo ou fechar o app cancela consultas pendentes. Consultas não desfazem salvamentos.
- Preferência: `%LOCALAPPDATA%\AniLista\anitsu-settings.json`.
- Sessão própria: `%LOCALAPPDATA%\AniLista\anitsu-profile`.
- Dependências: `%LOCALAPPDATA%\AniLista\runtime\webview2-1.0.4258.31`.
- Biblioteca e seu único `.bak` continuam na localização anterior e não guardam a sessão.
- O executável pode mudar de pasta. No modo extensão, use **Conectar / reparar** depois de movê-lo, porque o navegador precisa do caminho atual do Native Host.

## Verificação

`scripts/build.ps1 -Test all` verifica armazenamento, correspondências, cancelamento, framing, IPC e interface, usando bibliotecas temporárias. `node extensions/anitsu/tests.js` verifica a extensão com a API do navegador simulada. O teste manual autenticado consiste em conectar, adicionar um anime conhecido e conferir o caminho aberto. Login no app e login do navegador são sessões distintas.
