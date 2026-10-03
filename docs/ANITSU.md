# Anitsu Downloader dentro do AniLista

1. No cartão do anime, clique em **⋯ → Pesquisar no Anitsu**. Esta é a única opção do menu.
2. A janela abre diretamente no Cloud, sem barra ou rodapé adicionais. O nome é preenchido assim que o campo fica disponível. Uma correspondência exata e única abre a pasta; resultados ambíguos pedem escolher um caminho.
3. Se precisar entrar na conta, o app abre o login do Anitsu. Depois de entrar, abra o Cloud pelo próprio site; a pesquisa escolhida é retomada automaticamente. Esta sessão é própria do aplicativo.
4. Selecione os arquivos no painel do **Anitsu Downloader 1.6.8** e inicie o download.

Novas buscas reaproveitam o Cloud aberto. A espera de 300 ms da busca do site é removida no navegador integrado; consultas antigas são canceladas e pedidos simultâneos iguais compartilham uma resposta. O tempo de resposta da rede e do serviço continua dependendo do Anitsu.

Os downloads diretos usam Downloads ou o destino já configurado anteriormente. Arquivos existentes são preservados, com sufixo numérico em nomes repetidos. Adicionar ou editar animes não dispara buscas. Se a pasta não abrir, o app copia o caminho e mostra um aviso. Fechar a janela encerra downloads diretos em andamento; fechar o AniLista também fecha sua janela Anitsu.

## Downloader incorporado

O executável contém o arquivo `Anitsu-Downloader.user.js` da [release 1.6.8](https://github.com/LucasPreto0000/Anitsu-Downloader/releases/tag/v1.6.8), sem alterar seus bytes. SHA256: `b160100e1149777274a3ca5eceb824876a8de98f545b32b17dcf431eb374b4d6`. Autores: TheCyBee & Saitama; licença MIT declarada no userscript.

Um adaptador fornece as APIs GM dentro do WebView2. Não é necessário instalar Tampermonkey, carregar uma extensão nem manter uma pasta de scripts junto do EXE. Downloads diretos usam a sessão do próprio Cloud; respostas 401/403 são repassadas ao script para renovação da sessão. O modo AB Download Manager exige que esse programa externo esteja instalado e configurado. IDM fica oculto porque sua extensão de navegador não funciona no WebView2.

O Windows precisa do [WebView2 Runtime da Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/). O SDK e os loaders estão incorporados ao EXE; somente o Runtime é externo.

## Armazenamento

- Sessão própria: `%LOCALAPPDATA%\AniLista\anitsu-profile`.
- Destino escolhido: `%LOCALAPPDATA%\AniLista\anitsu-download-folder.txt`.
- Dependências extraídas automaticamente: `%LOCALAPPDATA%\AniLista\runtime\webview2-1.0.4258.31`.
- Biblioteca e seu único `.bak` continuam na localização anterior e não guardam a sessão.
- A sessão pertence ao perfil próprio do app. Mover o executável preserva a sessão e a biblioteca.

A extensão em `extensions/anitsu` pertence à versão 1.1.2 e não é usada pela interface atual.

## Verificação

`scripts/build.ps1 -Test all` verifica dados e interface com bibliotecas temporárias. `node tests/anitsu-embedded.js` verifica o adaptador GM e a confirmação da navegação. `scripts/test-anitsu-embedded.ps1` usa o Runtime real com respostas HTTPS simuladas e perfil temporário para verificar montagem do painel, busca, arquivo baixado, preservação de arquivos existentes e erro 401. Esse teste não autentica a conta real do usuário; login e downloads reais dependem de uma sessão válida e do serviço Anitsu.
