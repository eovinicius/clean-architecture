---
title: E-mail do destinatário no log e senha do cadastro sem limite máximo
tags: [inbox, security, lgpd, achado]
---

# E-mail do destinatário no log e senha do cadastro sem limite máximo

Achados de 2026-10-25, durante a escrita de [[SPEC-0003-confirmacao-de-email]] e [[SPEC-0004-recuperacao-de-senha]]. **Não corrigidos**: estão fora do escopo dessas specs.

## 1. `LoggingEmailSender` registra o e-mail do destinatário

- `app/src/Infrastructure/Notifications/LoggingEmailSender.cs` faz `logger.LogInformation("Email to {Recipient}: {Subject}", recipient, subject)`.
- E-mail é dado pessoal. Isso contradiz `.claude/rules/security.md` (LGPD: dados de usuários fora de logs).
- As SPEC-0003 e SPEC-0004 aumentam o número de envios (confirmação, reenvio e redefinição), então o volume de e-mails nos logs cresce.
- Correção sugerida: tirar o destinatário do log, ou registrar só um identificador não pessoal (ex.: `UserId`, se ele passar a ser recebido). É bugfix sem mudança de contrato, então dispensa spec.

## 2. Senha do cadastro sem tamanho máximo

- `app/src/Application/Users/Register/RegisterUserCommandValidator.cs` valida a senha só com `NotEmpty().MinimumLength(8)`.
- Uma senha enorme custa CPU no `PasswordHasher` (risco de DoS) e contradiz a regra de que toda entrada externa tem limite de tamanho (`security.md`).
- A SPEC-0004 adota o máximo de 128 caracteres para a senha nova. O cadastro e o login ficam diferentes até que outra mudança alinhe os dois. É mudança de validação visível no contrato (400 para senha longa), então precisa de decisão.
