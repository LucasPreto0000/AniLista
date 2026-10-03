# Anitsu Downloader dentro do AniLista

1. No cartão do anime, clique em **⋯ → Pesquisar no Anitsu**.
2. Na primeira vez, use **Entrar no Anitsu** e faça login no site verdadeiro. Esta sessão é própria do aplicativo; o login do Chrome/Edge não é transferido.
3. Clique em **Pesquisar** após entrar. Uma correspondência exata e única abre a pasta; resultados ambíguos pedem escolher um caminho.
4. Selecione os arquivos no painel flutuante do **Anitsu Downloader 1.6.8** e inicie o download. **⋯ → Abrir Anitsu Downloader** também abre o Cloud sem pesquisar.
5. **Pasta dos downloads** muda o destino dos downloads diretos. O padrão é Downloads do usuário. Arquivos existentes são preservados, com sufixo numérico nos novos arquivos de mesmo nome.

Adicionar, editar ou importar animes não dispara pesquisas. Falhas no Anitsu não alteram a biblioteca. Se a interface do site mudar e impedir abrir a pasta, **Copiar caminho** continua disponível. Fechar a janela encerra downloads diretos em andamento; fechar o AniLista também fecha sua janela Anitsu.

## Downloader incorporado

O executável contém o arquivo `Anitsu-Downloader.user.js` da [release 1.6.8](https://github.com/LucasPreto0000/Anitsu-Downloader/releases/tag/v1.6.8), sem alterar seus bytes. SHA256: `b160100e1149777274a3ca5eceb824876a8de98f545b32b17dcf431eb374b4d6`. Autores: TheCyBee & Saitama; licença MIT declarada no userscript.

Um adaptador fornece as APIs GM dentro do WebView2. Não é necessário instalar Tampermonkey, carregar uma extensão nem manter uma pasta de scripts junto do EXE. Downloads diretos usam a sessão do próprio Cloud; respostas 401/403 são repassadas ao script para renovação da sessão. O modo AB Download Manager exige que esse programa externo esteja instalado e configurado. IDM fica oculto porque sua extensão de navegador não funciona no WebView2.

O Windows precisa do [WebView2 Runtime da Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/). O SDK e os loaders estão incorporados ao EXE; somente o Runtime é externo.

## Armazenamento

- Sessão própria: `%LOCALAPPDATA%\AniLista\anitsu-profile`.
- Destino escolhido: `%LOCALAPPDATA%\AniLista\anitsu-download-folder.txt`.
- Dependências extraídas automaticamente: `%LOCALAPPDATA%\AniLista\runtime\webview2-1.0.4258.31`.
- Biblioteca e seu único `.bak` continuam na localização anterior e não guardam a sessão.
- **Sair da conta** limpa os dados do perfil Anitsu. Mover o executável preserva a sessão e a biblioteca.

A extensão em `extensions/anitsu` pertence à versão 1.1.2 e não é usada pela interface atual.

## Verificação

`scripts/build.ps1 -Test all` verifica dados e interface com bibliotecas temporárias. `node tests/anitsu-embedded.js` verifica o adaptador GM e a confirmação da navegação. `scripts/test-anitsu-embedded.ps1` usa o Runtime real com respostas HTTPS simuladas e perfil temporário para verificar montagem do painel, busca, arquivo baixado, preservação de arquivos existentes e erro 401. Esse teste não autentica a conta real do usuário; login e downloads reais dependem de uma sessão válida e do serviço Anitsu.
