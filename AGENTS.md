# Desenvolvimento do AniLista

Aplicativo Windows Forms em C#, .NET Framework 4.8. O build restaura o SDK WebView2 automaticamente pelo feed oficial NuGet; não requer instalar o SDK .NET nem o cliente NuGet. Testes da extensão usam Node.js sem pacotes adicionais.

## Comece pelo necessário

- Leia `docs/DEVELOPMENT.md` para localizar a funcionalidade; abra apenas os arquivos relevantes.
- Use `rg` para localizar símbolos. Ignore `.git`, `bin`, `qa` e executáveis nas buscas.
- Preserve alterações locais que não pertençam à tarefa. Siga o estilo do arquivo editado; evite reformatações gerais.
- Reaproveite `Theme`, os controles existentes e `DpiForm` para tema, cursores e escala.

## Compilar e verificar

- Compilar: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1`
- Armazenamento, catálogo ou capas: acrescente `-Test core`.
- Interface, interações ou DPI: acrescente `-Test ui`.
- Mudanças em ambos, no build ou preparação de publicação: acrescente `-Test all`.
- O script reutiliza binários quando os arquivos necessários não mudam. Use `-Force` apenas quando necessário.
- O resultado fica em `bin/AniLista.exe`. Para atualizar o executável na raiz, feche o aplicativo e use `-Install`.
- Confira `git diff --check`. Não repita testes aprovados sem novas mudanças ou uma dúvida concreta.

## Dados do usuário

- A biblioteca fica em `%LOCALAPPDATA%/AniLista/biblioteca.json`; há somente um backup, `biblioteca.json.bak`.
- Testes usam diretórios temporários próprios. Nunca use a biblioteca real como fixture.
- Atualizar ou reverter código/executável não deve alterar nem restaurar a biblioteca do usuário.
- Preserve gravação atômica, validação e detecção de dados desatualizados em `LibraryStore`.
- Não encerre o aplicativo à força para instalar uma compilação; primeiro tente fechar a janela normalmente.

## Publicação

Envie ao GitHub ou publique uma release quando o usuário solicitar. A sequência de versionamento e publicação está em `docs/DEVELOPMENT.md`.
