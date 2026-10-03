# Integração AniLista e Anitsu

Data: 03/10/2026. Estado: desenho para revisão; implementação ainda não iniciada.

## Objetivo autorizado

Após adicionar um anime com sucesso ao AniLista, pesquisar seu título no Anitsu.
Quando houver uma correspondência confiável, abrir o Anitsu no navegador.
Oferecer dois modos selecionáveis: login próprio no AniLista e extensão que
aproveita o login existente no navegador. Apenas um modo executa a pesquisa.

A biblioteca, o backup único, os cursores e o layout atual dos cartões devem
permanecer preservados. Editar, importar, recuperar backups e incrementar
episódios não devem iniciar pesquisas automáticas.

## Evidências e limites do site

- O frontend do Anitsu Cloud chama `GET /api/search?q=<texto>` na mesma origem.
- Sem login, esse endpoint respondeu HTTP 401. O site instrui o usuário a entrar
  pelo site principal `https://anitsu.moe/`.
- O frontend consome `results`, com campos `name`, `path` e `kind`.
  O contrato completo ainda precisa de validação com uma sessão autorizada.
- A seleção de uma pasta chama `/api/files?path=...` e atualiza o estado da
  página. O código analisado não oferece links diretos para pastas.
- O WebView2 Runtime já está instalado no computador. O perfil de um WebView2
  será próprio do AniLista; não será o perfil do navegador do usuário.

Esses detalhes não são uma API oficial documentada do Anitsu. A integração deve
isolar o contrato do site em um adaptador e tratar mudanças como indisponibilidade.
Não fabricar URLs de pastas que o site não interpreta.

## Experiência comum aos dois modos

Adicionar uma configuração `Anitsu` usando Theme, DpiForm e os controles atuais.
O usuário escolhe `Desativado`, `Login no AniLista` ou `Extensão no navegador`.
O recurso começa desativado até a configuração de um modo, para não abrir
navegadores nem solicitar autenticação antes da escolha.

Após o salvamento confirmado, enfileirar a pesquisa do título efetivamente
salvo. Fechar o cadastro normalmente e executar a pesquisa sem bloquear a UI.
Mostrar progresso e resultado em uma área de feedback que não sobrescreva
avisos de falha no backup. Falhas de busca nunca desfazem o anime salvo.

Resultados:

- Correspondência única e confiável: abrir o destino disponível no modo ativo.
- Vários candidatos ou semelhança incerta: exibir lista de nomes e caminhos para
  escolha; não escolher automaticamente outra temporada.
- Nenhum resultado: informar `Anime salvo. Não encontrado no Anitsu.`
- Login ausente ou expirado: informar que o anime foi salvo e oferecer reconexão.
- Site indisponível: informar a falha e oferecer tentativa manual.

Permitir pesquisar novamente um anime existente por uma ação explícita. Não
abrir o navegador duas vezes para o mesmo resultado da mesma operação.

## Modo 1: login no AniLista

Uma janela WebView2 apresenta o site verdadeiro de login do Anitsu. O usuário
digita suas credenciais diretamente no site. O AniLista não solicita senha em
formulários próprios nem extrai cookies do navegador existente.

Manter o perfil do WebView2 em `%LOCALAPPDATA%/AniLista/Anitsu/WebView2` e as
preferências em arquivo próprio, separado de `biblioteca.json`. A sessão pode
persistir entre aberturas. Oferecer desconectar/limpar a sessão deste modo.

Pesquisar dentro do contexto autenticado da origem Anitsu Cloud. As credenciais
continuam no perfil do WebView2; retornam ao coordenador apenas resultados
limitados ou estados de erro. Não enviar cookies para a extensão ou para logs.

Ao encontrar o anime, abrir `https://nuvem.anitsu.moe/` no navegador padrão e
mostrar no AniLista o nome e o caminho encontrados. Disponibilizar copiar esse
caminho por ação explícita. **Sem a extensão, este modo não promete abrir a
pasta diretamente no navegador**, pois o site não oferece um link para isso.
O navegador externo pode pedir login se não tiver uma sessão própria.

Iniciar o WebView2 apenas para conectar ou pesquisar. Dispensar a instância
quando o serviço estiver ocioso e ao fechar o app. Não instalar silenciosamente
um runtime ausente: apresentar a dependência e o link oficial, se necessário.

## Modo 2: extensão para Chrome e Edge

Criar extensão Manifest V3 restrita ao Anitsu Cloud e uma ponte Native Messaging.
O código da extensão usa o contexto autenticado do site no navegador existente.
Não ler o banco de cookies, não usar a permissão `cookies`, não abrir perfil
privado do navegador e não transmitir sessões para o AniLista.

A extensão será entregue como pasta local para carregar no modo de desenvolvedor.
Publicação nas lojas não faz parte desta entrega. Instalação/carregamento e
permissões do navegador exigem interação do usuário. Uma chave pública fixa no
manifesto garante ID estável para o registro Native Messaging.

A ponte será um modo dedicado do executável AniLista. O navegador inicia esse
modo antes da inicialização dos formulários. A ponte valida a origem exata da
extensão e comunica-se com a instância aberta do aplicativo por named pipe
restrito ao usuário atual. Não abrir uma porta TCP nem exigir privilégios de
administrador. Mensagens são JSON UTF-8 com limites, IDs de operação e timeout.

O cadastro Native Messaging usa somente as chaves HKCU correspondentes a Chrome
e/ou Edge selecionados pelo usuário, com opção de remover esse cadastro. Mudar
o executável de pasta exige reparar o caminho do registro. A biblioteca continua
na localização existente.

Conectar pela ação da extensão ou pela inicialização do navegador. Mostrar no
AniLista se a extensão está conectada; se não estiver, orientar a abrir o
navegador e clicar na extensão. Não lançar processos auxiliares em loop para
tentar conectar. Ao fechar o AniLista, a ponte encerra-se.

Na pesquisa, reaproveitar uma aba do Anitsu Cloud quando possível. Se necessário,
criar uma aba inativa para pesquisar. A execução do script usa a origem do site,
com parâmetros serializados, sem concatenar o título como código JavaScript.
Retornar apenas resultados, nunca credenciais.

Quando uma correspondência for confirmada, ativar a aba e usar a busca/seleção
da própria interface do Anitsu para navegar até o caminho encontrado. Centralizar
os seletores DOM e usar esperas limitadas. Se a interface mudar, reportar a falha
e apresentar o caminho; não afirmar que a pasta foi aberta.

Se a aba foi criada pela operação e não houver resultado, fechá-la. Nunca fechar
uma aba previamente aberta pelo usuário. Se houver login pendente, ativá-la para
o usuário entrar no site, com opção de pesquisar novamente.

## Componentes e responsabilidades

- `AnitsuSettings`: preferências de modo, persistência própria e validação.
- `AnitsuSearchCoordinator`: fila, cancelamento, feedback e abertura de resultados.
- `IAnitsuSearchProvider`: contrato comum para os dois modos e testes simulados.
- `AnitsuMatcher`: normalização e seleção de candidatos independente de rede/UI.
- `AnitsuWebViewProvider` e janela de conexão: sessão própria e consultas do site.
- `AnitsuNativeBridge`: protocolo stdio do navegador e IPC com a instância aberta.
- `extensions/anitsu/`: manifesto, service worker, scripts da página e configuração.
- `MainForm.SaveEntry`: acionar o coordenador somente depois de `SaveList` retornar
  sucesso em uma inclusão nova; manter o fluxo de erro atual.

Manter os dois transportes independentes do armazenamento da biblioteca. Não
modificar o esquema de Anime/Library para guardar autenticação ou resultados.
Pesquisar pelo título salvo; aliases do catálogo disponíveis durante a inclusão
podem ser usados como consultas secundárias, sem requisições ilimitadas.

## Correspondência, cancelamento e validação

Normalizar caixa, espaços, diacríticos e pontuação na comparação, preservando
números, indicadores de temporada e sufixos significativos. Remover apenas
rótulos de release claramente identificados. Não transformar automaticamente
`Infinite Stratos 2` em `Infinite Stratos`.

Correspondência automática exige nome normalizado igual ao título ou a um alias
conhecido. Se existirem várias pastas com o mesmo nome em categorias diferentes,
exigir seleção. Busca aproximada apresenta candidatos, sem abrir automaticamente.

Aceitar somente dados no formato esperado e destinos HTTPS nos hosts Anitsu
autorizados. `path` é dado do site, nunca um caminho de arquivo local ou comando.
Limitar tamanho de resposta, quantidade de candidatos e duração da operação.
Cancelar operações ao fechar o app ou trocar o modo e rejeitar resultados atrasados.

## Build e distribuição

Adicionar dependência WebView2 de versão fixa por restauração reproduzível do
pacote oficial e documentar essa nova dependência no guia de desenvolvimento.
Manter .NET Framework 4.8 e o build PowerShell existente.

Preservar a distribuição de um único AniLista.exe incorporando as bibliotecas
gerenciadas e o loader nativo necessário como recursos e carregando-os de uma
pasta versionada própria em LocalAppData. Não sobrescrever o perfil de sessão
ao instalar ou reverter o executável. A extensão é uma entrega adicional opcional.

Acrescentar referências e recursos ao fingerprint do build. Atualizar o patch
da versão, changelog e documentação quando a implementação estiver pronta.
Não publicar no GitHub sem solicitação desta entrega.

## Verificação e critérios de entrega

Testes com provedores simulados devem cobrir inclusão salva antes da busca,
salvamento recusado sem busca, edição sem busca, duplicata sem busca, falhas de
login/rede, correspondência ambígua, distinção entre temporadas, cancelamento e
mensagens IPC inválidas. Diretórios de testes próprios; nunca a biblioteca real.

Testar o protocolo Native Messaging, origin allowlist, limites de mensagem,
desconexão e encerramento da ponte. Testar extensão com respostas simuladas e
verificar que só ativa a aba depois de um resultado confirmado ou login pendente.

Executar `scripts/build.ps1 -Test all` e `git diff --check`. Inspecionar a UI
relacionada em escalas de DPI existentes. Verificar preservação da biblioteca e
do backup por hashes, sem usar seu conteúdo como fixture.

A verificação real autenticada de cada modo exige o login realizado pelo usuário.
Se essa etapa ficar pendente, informar exatamente quais fluxos foram simulados e
quais ainda não foram confirmados com o Anitsu ao vivo. Atualizar o executável em
Documentos/AniList somente com o app fechado normalmente.

## Referências

- Anitsu Cloud: https://nuvem.anitsu.moe/
- Native Messaging Chrome: https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging
- Native Messaging Edge: https://learn.microsoft.com/en-us/microsoft-edge/extensions/developer-guide/native-messaging
- WebView2 WinForms: https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winforms
- Perfis WebView2: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder
