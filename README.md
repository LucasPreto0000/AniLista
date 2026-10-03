# AniLista

Aplicativo Windows para organizar sua biblioteca pessoal de animes, com busca no catálogo da AniList, capas e acompanhamento de episódios.

## Download

Baixe **AniLista.exe** em **[Releases](https://github.com/LucasPreto0000/AniLista/releases)** e abra com dois cliques no Windows. Requer .NET Framework 4.8.

O aplicativo pode ser usado sem instalação e sem abrir um navegador.

## Compilar o código-fonte

Para compilar manualmente, baixe o código-fonte, extraia no Windows e execute `COMPILAR.bat`. O script usa o compilador do .NET Framework instalado no Windows e gera `AniLista.exe`.

Para desenvolvimento, use `scripts/build.ps1`: ele compila em `bin/`, reaproveita resultados sem alterações e permite escolher `-Test core`, `-Test ui` ou `-Test all`. O [guia de desenvolvimento](docs/DEVELOPMENT.md) contém os comandos e o mapa das funcionalidades.

## Como usar

1. Clique em **Adicionar anime**.
2. Digite o nome. Os resultados aparecem enquanto você digita, com a capa à esquerda. Use a seta para baixo ou clique duas vezes para escolher e selecione a lista. **Buscar** e **Enter** também funcionam. Clique em **Carregar mais** para ver a próxima página. Enquanto uma busca está em andamento, a seleção fica bloqueada.
3. Em **Assistindo**, registre seu episódio atual. **+1 episódio** avança um episódio.
4. Use **Editar** para mudar o episódio, o total ou a lista.
5. Em **Quero assistir**, **Começar a assistir** move o anime para **Assistindo**.
6. Ao chegar ao último episódio informado, o anime vai para **Concluídos**. Você também pode escolher essa lista diretamente no formulário.
7. O cadastro manual funciona sem internet. Total **0** significa total não informado.

## Listas

| Lista | Finalidade |
| --- | --- |
| Quero assistir | Animes que você pretende assistir |
| Assistindo | Animes em andamento, com o episódio atual |
| Concluídos | Animes que você já terminou |

## Salvamento e funcionamento offline

As alterações são salvas automaticamente neste computador, em `%LOCALAPPDATA%\AniLista\biblioteca.json`, fora da pasta do executável. Um único backup completo, `biblioteca.json.bak`, é atualizado a cada salvamento. Não são criados arquivos `.bak.1`, `.bak.2` nem um histórico de cópias.

Mover, atualizar ou reverter o executável não muda essa pasta: o aplicativo continua lendo o mesmo AppData do usuário. Se o arquivo principal estiver ausente ou corrompido, o aplicativo recupera o backup válido. Uma instância com dados antigos não pode sobrescrever uma biblioteca que mudou no disco.

Use **Backup e recuperação**, no rodapé, para exportar a biblioteca ou recuperar animes do backup automático ou de outro arquivo. A recuperação adiciona apenas animes ausentes, preservando os episódios e as listas dos animes atuais.

A busca e o download das capas precisam de internet. Suas listas, episódios e o cadastro manual funcionam offline. O catálogo é fornecido pela AniList. Consultas ficam em cache por cinco minutos. Se a API limitar os pedidos, a busca mostra a espera e pode ser cancelada ao mudar o texto. Capas só são baixadas quando os cartões aparecem na área visível.

## Atalhos

| Atalho | Ação |
| --- | --- |
| Ctrl+N | Abrir Adicionar anime |
| Ctrl+F | Ir para o filtro por nome |
| Esc | Limpar o filtro |

## Código-fonte

O código-fonte está na pasta `source`, separado por responsabilidade: formulários e controles, `LibraryStore`, `CatalogClient`, `CoverService` e ajustes de DPI. O script `COMPILAR.bat` gera um único executável usando C# e Windows Forms (.NET Framework 4.8).

Os testes estão em `tests`. O workflow Windows compila e executa testes de recuperação, backups, cache, paginação, limites da API, seleção, cartões, capas e escalas de 100%, 150% e 200%. A troca real entre monitores deve ser conferida no equipamento do usuário.

Para publicar uma versão, atualize `source/VersionInfo.cs`, crie as notas em `releases/vX.Y.Z.md`, atualize [CHANGELOG.md](CHANGELOG.md) e execute **Compilar e testar AniLista para Windows** em Actions, informando `vX.Y.Z`. A publicação exige testes aprovados, confere a versão do executável e recusa substituir uma release existente. Somente `AniLista.exe` é anexado; o GitHub fornece seus próprios arquivos de código-fonte.

O aplicativo organiza sua biblioteca e permite abrir o Anitsu Downloader integrado.

## Integração Anitsu

Use **⋯ → Pesquisar no Anitsu**, no cartão do anime. A busca abre o Cloud na área central da janela principal com **Anitsu Downloader 1.6.8 incorporado ao EXE**. Entre na sua conta nessa área e selecione os arquivos para baixar. Resultados ambíguos pedem escolher uma pasta; falhas na busca não afetam a biblioteca. A área integrada mostra o site diretamente, sem barra adicional, com preenchimento automático do nome e reaproveitamento da página nas próximas buscas. Use **← Biblioteca** ou uma lista da barra lateral para voltar.

Consulte [como usar](docs/ANITSU.md). Requer o WebView2 Runtime da Microsoft. Não exige extensão nem pasta de scripts ao lado do aplicativo. Downloads diretos vão para Downloads ou para o destino configurado anteriormente.

Cursores: Windows 11 Cursors Concept, por [jepriCreations](https://www.deviantart.com/jepricreations), o mesmo pacote usado no YT-DLP Deck. Licença original em `source/assets/cursors/LICENSE-cursors.txt`.

