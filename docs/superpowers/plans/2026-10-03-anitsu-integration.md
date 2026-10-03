# Anitsu Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pesquisar no Anitsu após adicionar um anime salvo, oferecendo login próprio e extensão do navegador como modos alternativos.

**Architecture:** Um coordenador desacoplado recebe inclusões confirmadas e usa um dos dois provedores. O primeiro pesquisa em um WebView2 autenticado; o segundo comunica-se com extensão Chrome/Edge por Native Messaging e named pipe do usuário. Biblioteca e backup permanecem independentes da integração.

**Tech Stack:** C# compatível com o csc do .NET Framework 4.8, Windows Forms, Microsoft.Web.WebView2 1.0.4258.31, extensão Manifest V3, PowerShell, testes C# e Node sem dependências adicionais para a extensão.

**Spec:** `docs/superpowers/specs/2026-10-03-anitsu-integration-design.md`, aprovada pelo usuário em 03/10/2026.

## Global Constraints

- Apenas um modo executa a pesquisa.
- Falhas de busca nunca desfazem o anime salvo.
- Não modificar o esquema de Anime/Library para guardar autenticação ou resultados.
- Não ler o banco de cookies, não usar a permissão `cookies`, não abrir perfil privado do navegador e não transmitir sessões para o AniLista.
- Não fabricar URLs de pastas que o site não interpreta.
- Não publicar no GitHub sem solicitação desta entrega.
- Manter .NET Framework 4.8 e o build PowerShell existente.
- O recurso começa desativado até a configuração de um modo.
- Testes usam diretórios próprios; nunca a biblioteca real.
- Preservar o único `biblioteca.json.bak`, a gravação atômica e todos os ajustes locais anteriores à tarefa.
- Encerrar recursos WebView2/IPC ao fechar o app; auxiliares não continuam em segundo plano após ele fechar.

## Review Focus

- Usuário fecha o cadastro, troca o modo ou fecha o app durante a busca: ignorar respostas antigas e não abrir abas tardias (tarefas 2 e 7).
- Pasta da segunda temporada tem nome semelhante à primeira: nunca abrir automaticamente uma temporada diferente (tarefa 1).
- Sessão do navegador expira entre a pesquisa e a abertura: informar login pendente sem perder o anime nem fechar uma aba do usuário (tarefa 5).
- Executável muda de pasta após registro da extensão: detectar e oferecer reparar o cadastro HKCU, sem pedir administrador (tarefas 4 e 6).
- API devolve HTML, payload excessivo, path estranho ou resultados duplicados: falha legível, limites de tamanho e seleção explícita (tarefas 1, 3 e 5).

## Mapa de arquivos e contratos

Novos componentes C# ficam em `source/Anitsu/`, compilados pelo glob recursivo já existente.
Classes públicas permitem testes sem abrir sessões reais. Não expor cookies nas interfaces.

| Arquivo | Responsabilidade |
| --- | --- |
| `Contracts.cs` | Modos, estados, candidatos, resultados e contrato dos provedores |
| `AnitsuMatcher.cs` | Normalizar títulos e escolher correspondências |
| `AnitsuSettingsStore.cs` | Preferências e paths próprios em LocalAppData |
| `AnitsuSearchCoordinator.cs` | Fila, cancelamento, callbacks de UI e deduplicação |
| `AnitsuWebViewProvider.cs` | Sessão WebView2 e API da origem do site |
| `AnitsuWebViewForm.cs` | Login no site verdadeiro |
| `WebViewDependencies.cs` | Recursos incorporados e loader versionado |
| `AnitsuNativeProtocol.cs` | Framing JSON e validação das mensagens |
| `AnitsuNativeHost.cs` | Modo stdio iniciado pelo navegador |
| `AnitsuBridgeServer.cs` | Named pipe e provedor da extensão |
| `AnitsuExtensionRegistration.cs` | Manifestos Native Messaging e registro HKCU |
| `AnitsuSettingsForm.cs` | Seleção do modo, conectar, desconectar e instruções |
| `AnitsuResultsForm.cs` | Escolha de resultado, caminho encontrado e copiar |
| `ExtensionIdentity.cs` | Identidade estável derivada da chave pública da extensão |
| `extensions/anitsu/` | Manifesto, scripts, instalação e testes da extensão |

Valores operacionais: título com até 180 caracteres; até 100 candidatos;
payload de até 512 KiB; path de até 2.048 caracteres; timeout de consulta de
20 segundos; abrir UI de autenticação somente por ação explícita ou aviso
de login do modo selecionado. Uma fila serial suporta até 10 inclusões pendentes.
Domínios autorizados de pesquisa e navegação: HTTPS `nuvem.anitsu.moe` e
HTTPS `anitsu.moe` para login. Nunca executar um path retornado como comando.

## Preparação de execução

- [ ] Aplicar `superpowers:using-git-worktrees` e preparar checkout isolado com o HEAD e os ajustes locais necessários, preservando a raiz de Documentos/AniList.
- [ ] Registrar baseline de fonte, executável e hashes dos arquivos de biblioteca existentes sem ler seu conteúdo. Não copiar dados reais para o checkout de testes.
- [ ] Transferir os ajustes locais de fonte/documentação/build para o checkout e criar snapshot local da base, sem publicar nem incluir binários ou perfis de autenticação.
- [ ] Executar build e testes existentes uma vez no checkout para identificar falhas de base antes das alterações.

### Task 1: Contratos, preferências e correspondência

**Files:** criar `source/Anitsu/Contracts.cs`, `AnitsuMatcher.cs`, `AnitsuSettingsStore.cs`; criar `tests/AnitsuTests.cs`; modificar `scripts/build.ps1` para incluir essa suíte em `-Test core` e `-Test all`.

**Interfaces:**
- `enum AnitsuMode { Disabled, AppLogin, BrowserExtension }`.
- `enum AnitsuSearchState { Found, NotFound, LoginRequired, Unavailable }`.
- `AnitsuCandidate` contém `string Name`, `string Path`, `string Kind`.
- `AnitsuSearchResult` contém `AnitsuSearchState State`, `List<AnitsuCandidate> Candidates`, `string Message`.
- `IAnitsuSearchProvider : IDisposable` fornece `Task<AnitsuSearchResult> SearchAsync(string title, CancellationToken token)` e `Task OpenAsync(AnitsuCandidate candidate, CancellationToken token)`.
- `AnitsuMatcher.Match(string title, IEnumerable<string> aliases, IEnumerable<AnitsuCandidate> candidates)` retorna candidatos exatos; candidatos aproximados continuam disponíveis para seleção explícita.
- `AnitsuSettingsStore(string folder)` fornece `AnitsuMode Load()` e `void Save(AnitsuMode mode)`; default Disabled e arquivo separado `anitsu-settings.json`.

- [ ] Escrever testes `SeasonNumbersStayDistinct`, `PunctuationAndAccentsMatch`, `DuplicatePathsRequireChoice`, `InvalidAndOversizedCandidatesRejected`, `SettingsDoNotChangeLibrary`, `BrokenSettingsDefaultToDisabled`. Assert: título `Infinite Stratos 2` não seleciona `Infinite Stratos`; duas pastas distintas permanecem duas escolhas; biblioteca e backup mantêm seus bytes.
- [ ] Executar `scripts/build.ps1 -Test core`; confirmar falha pela ausência das interfaces ou comportamento, sem usar serviços reais.
- [ ] Implementar os contratos e validações descritos, normalizando texto com Unicode FormD e preservando números e temporadas.
- [ ] Reexecutar a suíte até os testes novos e existentes passarem.
- [ ] Commit somente arquivos desta tarefa no checkout isolado.

### Task 2: Coordenador e fila após inclusão

**Files:** criar `source/Anitsu/AnitsuSearchCoordinator.cs`; testes em `tests/AnitsuTests.cs`.

**Interfaces:**
- Construtor recebe `Func<AnitsuMode> getMode`, `Func<AnitsuMode,IAnitsuSearchProvider> getProvider`, `Action<Action> dispatch`, `Action<string> feedback` e `Func<IList<AnitsuCandidate>,Task<AnitsuCandidate>> choose`.
- `void OnSavedAddition(Anime anime)` enfileira cópia; `void Cancel()` cancela geração atual; `Dispose()` cancela e libera recursos.
- Provider.OpenAsync é chamado apenas após correspondência única ou escolha explícita; modo Disabled não chama provider.

- [ ] Escrever `DisabledModeMakesNoRequests`, `OneAdditionOpensOnce`, `MultipleResultsRequireChoice`, `NoResultDoesNotOpen`, `ProviderFailureKeepsSavedAnime`, `ModeChangeIgnoresLateResponse`, `QueueDoesNotOpenAfterDispose`, com provedores e dispatcher simulados.
- [ ] Executar testes e confirmar falha antes da implementação.
- [ ] Implementar fila serial limitada, IDs de operação, captura de falhas de rede e cancelamento por geração. Não mostrar modal para cada erro e não sobrescrever aviso de backup.
- [ ] Executar `scripts/build.ps1 -Test core`; commit da tarefa.

### Task 3: Sessão própria e dependência WebView2

**Files:** criar `AnitsuWebViewProvider.cs`, `AnitsuWebViewForm.cs`, `WebViewDependencies.cs`, `scripts/restore-webview2.ps1`; modificar `scripts/build.ps1`, `.gitignore`; testes em `tests/AnitsuTests.cs` e `tests/InteractionTests.cs`.

**Interfaces:**
- `AnitsuWebViewProvider : IAnitsuSearchProvider`, sessão em `<settingsFolder>/Anitsu/WebView2`.
- `Task ConnectAsync(IWin32Window owner)` mostra login; `Task DisconnectAsync()` limpa apenas esse perfil.
- `WebViewDependencies.Prepare()` resolve assemblies incorporados e loader para diretório versionado; não lê perfis Chrome/Edge.
- API do site fica num adaptador com script versionado, `fetch('/api/search?q=' + encodeURIComponent(title))`, `credentials: 'same-origin'` e parâmetros serializados.

- [ ] Escrever testes de interpretação para 200/401/429/500, JSON errado, HTML no lugar de JSON, limite de 512 KiB, Unicode e cancelamento; testar ausência de runtime com mensagem legível.
- [ ] Confirmar falhas; restaurar pacote oficial fixo `Microsoft.Web.WebView2/1.0.4258.31`, verificar integridade pelo SHA-512 publicado pelo feed NuGet e extrair apenas assemblies e loader necessários. Cache em `bin/dependencies/`, ignorado pelo Git.
- [ ] Incorporar dependências no executável, incluir pacote/resources no fingerprint do build e preparar loader x86/x64 conforme processo. A implantação não pede instalação silenciosa de runtime.
- [ ] Implementar login no site real e consulta no contexto autenticado. Carregamento de dependências é tardio: native host e modo Disabled não iniciam WebView2.
- [ ] OpenAsync neste modo abre somente a página HTTPS Anitsu Cloud no navegador padrão e apresenta o caminho encontrado; não inventa deep link.
- [ ] Executar core/ui com transporte simulado e screenshot da janela de conexão; commit da tarefa. Login real fica para validação com o usuário.

### Task 4: Ponte Native Messaging e registro por usuário

**Files:** criar `AnitsuNativeProtocol.cs`, `AnitsuNativeHost.cs`, `AnitsuBridgeServer.cs`, `AnitsuExtensionRegistration.cs`, `ExtensionIdentity.cs`; modificar `source/Program.cs`; testes em `tests/AnitsuTests.cs`.

**Interfaces:**
- `AnitsuNativeProtocol.Read(Stream stream)` retorna mensagem validada; `Write(Stream stream, object message)` usa uint32 little-endian + JSON UTF-8, máximo 512 KiB.
- `AnitsuNativeHost.TryRun(string[] args, out int exitCode)` reconhece a origem exata da extensão antes de DPI, mutex e UI. Rejeita argumentos inválidos.
- `AnitsuBridgeServer : IAnitsuSearchProvider` oferece `bool IsConnected`, evento `ConnectionChanged` e `Start()`; IPC restrito ao SID atual com PipeSecurity.
- `AnitsuExtensionRegistration.Register(string exePath, string extensionId, bool chrome, bool edge)` e `Unregister(bool chrome, bool edge)` alteram somente `HKCU/Software/{Google/Chrome|Microsoft/Edge}/NativeMessagingHosts/br.anilista.anitsu`.
- Manifesto nativo em `<settingsFolder>/Anitsu/native-host.json` aponta para AniLista.exe e contém somente a origem da extensão aprovada.

- [ ] Escrever testes para framing fragmentado, EOF, zero/negativo/excessivo, JSON inválido, timeout, origem desconhecida, IDs duplicados, conexão de usuário diferente e encerramento do app sem auxiliar persistente.
- [ ] Testar registro por meio de abstração fake de registry: nenhum acesso HKLM; mudança de path detectada e reparável.
- [ ] Confirmar testes falhando; implementar forwarding duplex entre stdio e pipe, com requestId, operações `hello/search/open/result/status`, validação e cancelamento.
- [ ] Conexão do bridge exige AniLista já aberto. Se não existir instância, retornar erro e sair; não abrir segunda UI nem lançar processos em loop.
- [ ] Executar core e teste do modo host em subprocesso com pipes; commit da tarefa.

### Task 5: Extensão Chrome/Edge autenticada

**Files:** criar `extensions/anitsu/manifest.json`, `background.js`, `site-adapter.js`, `README.md`, `tests/extension-anitsu.test.mjs`.

**Interfaces:**
- Chave pública fixa em manifest.json gera ID documentado e constante em ExtensionIdentity.cs; chave privada não é distribuída.
- Manifesto MV3 usa `nativeMessaging`, `scripting` e host `https://nuvem.anitsu.moe/*`; não usa `cookies`, `debugger` ou acesso a outros hosts.
- `searchInAnitsu(title)` no contexto da página retorna o contrato da Task 1.
- `openCandidate(candidate)` seleciona o resultado de path exato pela interface de busca do site, sem gerar URL inexistente.
- Mensagens do service worker ao host contêm operação, ID, resultados/status; nunca senha, cookies ou HTML arbitrário.

- [ ] Escrever testes Node com fakes do chrome API: aba existente preservada; aba nova inativa; inexistente fecha só aba criada; login ativa aba; resultado positivo ativa aba; IDs/reply inválidos rejeitados; 401 entre search/open pede reconexão.
- [ ] Rodar `node --test tests/extension-anitsu.test.mjs` e confirmar falhas.
- [ ] Implementar conexão pela ação da extensão e início do navegador, sem loops permanentes de reconexão. connectNative mantém a operação, e perda do app desconecta o helper.
- [ ] Pesquisar com fetch same-origin no contexto da aba. Validar retorno e selecionar no DOM pelo path, com timeout de 20 segundos. Mudança de layout gera fallback explícito com caminho encontrado.
- [ ] Reexecutar testes Node, verificar manifesto/identidade com testes C#, documentar carregamento local nos dois navegadores e commit da tarefa.

### Task 6: Configuração e escolha de resultados

**Files:** criar `AnitsuSettingsForm.cs`, `AnitsuResultsForm.cs`; modificar `source/MainForm.cs`; testes em `tests/InteractionTests.cs`.

**Interfaces:**
- `AnitsuSettingsForm` recebe settings e providers e permite Disabled/AppLogin/BrowserExtension, conectar/desconectar/registrar/reparar.
- `AnitsuResultsForm` recebe candidatos, retorna escolha ou cancelamento e oferece copiar caminho por clique explícito.
- MainForm recebe coordenador opcional para testes, sem mudar assinaturas chamadas pelos testes existentes.

- [ ] Escrever testes para default Disabled, troca de modo cancelando consulta antiga, lista ambígua cancelável, instalação da extensão com status claro, runtime ausente, foco/teclado/cursor e caminhos longos em 100/150/200% DPI.
- [ ] Confirmar falhas; implementar telas usando Theme e DpiForm sem refazer cartões/sidebar.
- [ ] Registro de extensão exige escolha explícita do navegador e instruções de carregar pasta local; não alterar registro na inicialização do app.
- [ ] Separar feedback Anitsu de aviso de biblioteca/backup. Não guardar preferências em biblioteca.json.
- [ ] Executar `scripts/build.ps1 -Test ui`, inspecionar screenshots relacionados e commit da tarefa.

### Task 7: Integração final no salvamento e ciclo de vida

**Files:** modificar `source/MainForm.cs`, `source/Program.cs`; testes em `tests/AnitsuTests.cs`, `tests/InteractionTests.cs`.

**Interfaces:** consumir OnSavedAddition e Cancel da Task 2; preservar SaveList e a interface EditorForm existente.

- [ ] Escrever testes `SuccessfulAddPersistsBeforeSearch`, `FailedSaveDoesNotSearch`, `DuplicateDoesNotSearch`, `EditImportAndEpisodeDoNotSearch`, `BackupWarningDoesNotUndoSearchOrDisappear`, `FinalEditedTitleIsQueried`, `WindowCloseDisposesBothProviders`.
- [ ] Confirmar falhas com LibraryStore temporário e providers simulados.
- [ ] Após SaveList retornar true para inclusão nova, notificar coordenador com cópia do anime efetivamente salvo e adiar UI de resultados até fechar o cadastro. Não disparar na carga da biblioteca.
- [ ] Inicializar bridge apenas no modo extensão e WebView2 apenas na conexão/consulta do modo login; tratar troca de modo e FormClosed com cancelamento e disposal.
- [ ] Executar core/ui e Node; commit da tarefa.

### Task 8: Revisão, documentação e entrega local

**Files:** `source/VersionInfo.cs`, `CHANGELOG.md`, `releases/v1.1.2.md`, `README.md`, `docs/DEVELOPMENT.md`, `docs/ANITSU.md`; artefato final `bin/AniLista.exe` e cópia na raiz.

- [ ] Atualizar versão 1.1.1.0 para 1.1.2.0 e registrar os dois modos, instalação local da extensão, login próprio, limites de navegação e dependência WebView2.
- [ ] Executar `scripts/build.ps1 -Test all`, testes Node e `git diff --check`; inspecionar imagens novas. Não repetir checks aprovados sem alteração nova.
- [ ] Solicitar uma revisão independente da implementação final, com foco em salvamento, protocolo, permissões e shutdown. Corrigir falhas relevantes e repetir apenas os checks afetados.
- [ ] Aplicar somente o delta desta tarefa ao projeto original após conferir que a base local não mudou durante a execução. Preservar todos os ajustes prévios fora da integração.
- [ ] Verificar hashes da biblioteca e backup em relação à baseline; alterações legítimas feitas pelo usuário durante o trabalho não devem ser sobrescritas.
- [ ] Fechar o AniLista normalmente se estiver aberto e executar build `-Install`; não encerrar à força. Entregar extensão local e instruções de login.
- [ ] Com o usuário, validar uma busca autenticada em cada modo e registrar se a etapa real ficou pendente. Simular falhas não equivale a declarar o Anitsu real validado.
- [ ] Encerrar com executável atualizado, testes executados e limitações reais. Não publicar nem enviar commits ao GitHub nesta entrega.

## Revisão do plano

- Todos os requisitos da especificação têm tarefa correspondente; os cinco riscos de Review Focus têm testes definidos.
- Os dois providers produzem o mesmo contrato, mas mantêm sessões separadas e navegação diferente conforme o limite do site.
- Nenhuma tarefa modifica LibraryStore para integrar login ou pesquisa.
- Os ajustes locais anteriores entram apenas como baseline de execução, preservados na raiz.
- Implementação depende da revisão deste plano e escolha do método de execução pelo usuário, conforme Superpowers.
