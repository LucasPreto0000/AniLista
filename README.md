# AniLista

Aplicativo Windows para organizar sua biblioteca pessoal de animes, com busca no catálogo da AniList, capas e acompanhamento de episódios.

## Download

Baixe **AniLista.exe** em **[Releases](https://github.com/LucasPreto0000/AniLista/releases)** e abra com dois cliques no Windows. Requer .NET Framework 4.8.

O aplicativo pode ser usado sem instalação e sem abrir um navegador.

## Compilar o código-fonte

Para compilar manualmente, baixe o código-fonte, extraia no Windows e execute `COMPILAR.bat`. O script usa o compilador do .NET Framework instalado no Windows e gera `AniLista.exe`.

## Como usar

1. Clique em **Adicionar anime**.
2. Digite o nome. Os resultados aparecem enquanto você digita, com a capa à esquerda. Use a seta para baixo ou clique duas vezes para escolher e selecione a lista. **Buscar** e **Enter** também funcionam.
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

As alterações são salvas automaticamente neste computador, em `%LOCALAPPDATA%\AniLista\biblioteca.json`. Uma cópia da versão anterior fica em `biblioteca.json.bak`.

A busca e o download das capas precisam de internet. Suas listas, episódios e o cadastro manual funcionam offline. O catálogo é fornecido pela AniList.

## Atalhos

| Atalho | Ação |
| --- | --- |
| Ctrl+N | Abrir Adicionar anime |
| Ctrl+F | Ir para o filtro por nome |
| Esc | Limpar o filtro |

## Código-fonte

O código-fonte está na pasta `source`. O script `COMPILAR.bat` gera o aplicativo Windows usando C# e Windows Forms.

O aplicativo organiza sua biblioteca; não transmite episódios de anime.
