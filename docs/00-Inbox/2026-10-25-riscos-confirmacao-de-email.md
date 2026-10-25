---
title: Riscos conhecidos da confirmação de e-mail
tags: [inbox, security, achado]
---

# Riscos conhecidos da confirmação de e-mail

Achados das revisões de arquitetura e segurança da [[SPEC-0003-confirmacao-de-email]] em 2026-10-25. **Não tratados** na implementação: precisam de decisão.

## 1. Pré-sequestro de conta (decisão de produto)

- Um atacante cadastra o e-mail da vítima com uma senha que ele conhece. A vítima recebe o e-mail de confirmação. Se ela confirmar (ou pedir reenvio e confirmar), a conta fica confirmada com a senha do atacante, que passa a conseguir fazer login.
- Hoje, os limites são a expiração em 7 dias (RN-16) e a recuperação de senha da [[SPEC-0004-recuperacao-de-senha]], que troca a senha e encerra as sessões. Mesmo assim, o JWT já emitido para o atacante vale até expirar.
- Opções:
  - o e-mail de confirmação avisa "se não foi você, ignore";
  - a confirmação exige a senha;
  - confirmar invalida a senha e obriga a definir uma nova;
  - aceitar o risco.

## 2. Falha do `IEmailSender` vira 500 e oráculo de enumeração

- O envio acontece depois do commit (limite aceito na Q-8 da SPEC-0003). Se o envio lança exceção, o cadastro devolve 500 com o Usuário já gravado.
- No reenvio, o 500 só ocorre para e-mail existente, não confirmado e fora dos 60 s, e por isso revela o estado da conta.
- Com o `LoggingEmailSender` atual, o envio não falha. O risco aparece com um provedor real: tratar junto com fila ou outbox.

## 3. Concorrência rara

- Duas confirmações **simultâneas com tokens diferentes** do mesmo Usuário (do cadastro e do reenvio): cada uma marca o próprio token e remove o outro. Um deadlock do Postgres viraria 500. A RN-4 só cobre o mesmo token.
- Dois reenvios simultâneos passam juntos pela checagem de 60 s (RN-6) e enviam dois e-mails. O rate limit por IP mitiga.

## 4. Recuperação de senha ([[SPEC-0004-recuperacao-de-senha]])

- **Tempo de resposta do `forgot-password`**: o ramo "e-mail existe" grava o token e envia o e-mail, e por isso é mais lento que o ramo "não existe". A resposta é idêntica, mas o tempo revela se o e-mail está cadastrado. É o mesmo limite aceito na Q-8 da SPEC-0003 e só se resolve tirando o envio do request (fila ou outbox).
- **Corrida na revogação de sessões**: o reset revoga os refresh tokens carregados. Um `POST /users/refresh-token` concorrente com um token antigo ainda válido pode gravar um token novo na mesma família logo depois dessa leitura, e esse token sobrevive ao reset. Para fechar de vez, o refresh precisaria checar uma versão da senha (ex.: `PasswordChangedAt`) ou o reset precisaria revogar por família. As duas opções mexem no fluxo de tokens, que só muda com pedido explícito (`60-security.md`).

## 5. Índice para a limpeza periódica

- O `DELETE` horário filtra `users` por `email_confirmed_at IS NULL AND registered_at < X`, sem índice. Hoje o custo é desprezível. Se a tabela crescer, cabe um índice parcial (`WHERE email_confirmed_at IS NULL`).
