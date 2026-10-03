# AniLista · Anitsu

**Legado da versão 1.1.2.** A interface atual usa o Downloader integrado pelo menu ⋯ dos cartões; esta extensão não é necessária nem recebe buscas do app atual. As instruções abaixo documentam a versão anterior. Consulte [o fluxo atual](../../docs/ANITSU.md).

Esta extensão pesquisa usando sua sessão do Anitsu no Chrome ou Edge. Cookies e credenciais permanecem no navegador.

## Instalação

1. Abra `AniLista.exe` e clique **Anitsu**.
2. Selecione **Extensão Chrome / Edge** e clique **Conectar / reparar**. O app registra a comunicação no seu usuário do Windows e abre esta pasta.
3. No navegador, abra `chrome://extensions` ou `edge://extensions` e ative **Modo do desenvolvedor**.
4. Clique **Carregar sem compactação** e selecione a pasta `extensions/anitsu` deste projeto.
5. Clique no ícone **AniLista · Anitsu**. Nas configurações do app deve aparecer **Extensão conectada**.

Adicione um anime no AniLista. Após salvar, ele consulta o Anitsu. Uma correspondência exata e única abre a pasta; mais de uma opção abre um seletor. O anime continua salvo mesmo se a busca falhar.

Mantenha o AniLista aberto enquanto usa a extensão. Se ele fechar ou você trocar de modo, a conexão encerra. Depois de reabrir, clique no ícone da extensão para conectar novamente. Se mover o executável, use **Conectar / reparar** para atualizar seu caminho.

Uma aba Anitsu existente é preservada. Sem aba aberta, a extensão cria uma aba inativa para pesquisar e fecha essa aba se não houver resultado. Caso precise de login, deixa a aba aberta. A autenticação pode ser feita normalmente no site.

**Desconectar** no app desativa a busca, remove o registro da comunicação e limpa apenas a sessão própria do WebView2. Seu login no navegador permanece no navegador. Para remover a extensão, use a página de extensões do Chrome/Edge.

## Desenvolvimento

`node extensions/anitsu/tests.js` executa os testes sem instalar pacotes. A chave pública no manifesto fixa a identidade; não há chave privada no projeto. Permissões: Native Messaging, abas e execução de scripts apenas nas origens Anitsu declaradas. Não usa permissão `cookies`.

O script de navegação utiliza a busca visível do Cloud e seleciona o caminho exato; não depende de URLs de pasta não suportadas. Mudanças da interface do site podem exigir atualizar o seletor. A abertura da pasta precisa ser confirmada com uma sessão autenticada do usuário.
