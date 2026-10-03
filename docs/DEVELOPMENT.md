# Guia rápido de desenvolvimento

## Onde alterar

| Funcionalidade | Arquivos e pontos de entrada |
| --- | --- |
| Biblioteca, listas e filtro | `source/MainForm.cs`: `BuildLayout`, `RenderCards`, `SaveList` |
| Card, episódios e ações | `source/AnimeCard.cs`: construtor, `Bind` |
| Buscar e selecionar anime | `source/SearchForm.cs`: `RunSearch`, `SelectResult`, `DrawResult` |
| Cadastro e edição | `source/EditorForm.cs` |
| Salvamento e backup único | `source/LibraryStore.cs`: `Load`, `Save`, `Install`, `WriteBackup` |
| Exportação e recuperação | `source/BackupForm.cs`, `LibraryStore.MergeMissing` |
| API, cache e limites | `source/CatalogClient.cs`: `SearchAsync` |
| Download e cache de capas | `source/CoverService.cs` |
| Cores, fontes e medidas | `source/Theme.cs` |
| Botões e controles reutilizáveis | `source/Controls.cs` |
| Escala entre monitores | `source/DpiForm.cs` |
| Cursores incorporados | `source/AppCursors.cs`, `source/assets/cursors` |
| Inicialização e instância única | `source/Program.cs` |
| Versão do executável | `source/VersionInfo.cs` |
| Integração Anitsu, sessão e IPC | `source/Anitsu/`, `extensions/anitsu/`, [configuração](ANITSU.md) |

## Comandos

Execute na raiz do projeto, com Windows PowerShell 5.1 ou PowerShell 7:

```powershell
# Compilar apenas o necessário; resultado em bin/AniLista.exe.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1

# Verificar dados, recuperação, API e capas.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Test core

# Verificar interface, ações, cursores e escala.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Test ui

# Verificação completa e instalação do executável na raiz (app fechado).
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Test all -Install
```

`-Force` recompila mesmo sem mudanças. O script compara o conteúdo dos arquivos e recompila também os testes quando o aplicativo muda. Documentação não provoca recompilação. Os testes selecionados sempre são executados; somente a compilação é reaproveitada.

O compilador é o `csc.exe` do .NET Framework instalado no Windows. Dependências, recursos e opções de compilação ficam em `scripts/build.ps1`, usado também pelo BAT e pelo GitHub Actions.

O build restaura WebView2 1.0.4258.31 do feed oficial NuGet e verifica o SHA512 fixado em `scripts/restore-webview2.ps1`. O hash foi calculado do pacote baixado por HTTPS; não é uma assinatura independente do editor. As duas assemblies e loaders x86/x64 são incorporados ao EXE. A primeira restauração requer internet. Testes da extensão: `node extensions/anitsu/tests.js`. O teste `AnitsuWebViewSmoke.cs` é manual e usa um perfil temporário para conferir o HTTP de autenticação real, sem acessar a biblioteca do usuário.

## Verificação visual

`tests/InteractionTests.cs` gera imagens em `qa/`, incluindo a biblioteca nas escalas de 100%, 150% e 200% e a tela de backups. Inspecione as imagens relacionadas à alteração. A troca física entre monitores exige verificação no equipamento do usuário.

## Dados e artefatos

- Código versionado: `source/`, `tests/`, `scripts/`, `docs/`.
- Saída local ignorada pelo Git: `bin/`, `qa/`.
- Biblioteca real: `%LOCALAPPDATA%/AniLista/biblioteca.json`.
- Backup automático único: `%LOCALAPPDATA%/AniLista/biblioteca.json.bak`.
- Testes de dados: `tests/CoreTests.cs`, com arquivos temporários e serviços simulados.
- Testes de interface: `tests/InteractionTests.cs`, com bibliotecas temporárias.

O executável pode mudar de pasta sem mudar a localização dos dados. Reversões de código devem preservar esses dados. Exportações manuais pertencem ao usuário.

## Publicar quando solicitado

1. Atualize `source/VersionInfo.cs`, `CHANGELOG.md` e `releases/vX.Y.Z.md`.
2. Execute `scripts/build.ps1 -Test all` e confira o diff.
3. Envie os arquivos de código ao GitHub.
4. Execute o workflow `build-windows.yml`, informando a versão correspondente.
5. Confira o resultado dos testes e o download do executável na release.

O workflow não substitui releases existentes. O executável é gerado a partir do código publicado.
