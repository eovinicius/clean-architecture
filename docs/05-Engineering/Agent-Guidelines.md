---
title: Diretrizes gerais para agentes de IA
status: Approved
tags: [engineering, ai, guidelines]
---

# Diretrizes gerais para agentes de IA

> Versão integral e portável das diretrizes que eram o `AGENTS.md` original. O `AGENTS.md` atual é o contrato enxuto deste projeto; este documento é lido **sob demanda** (ver [[Agent-Harness]]).


Guia para qualquer agente de IA que trabalhe nesta codebase (ou em outra que adote este arquivo). O objetivo é produzir mudanças que pareçam escritas pelo time: coerentes com os padrões existentes, mínimas, seguras e verificadas.

## 1. Precedência

1. Instruções explícitas do usuário na tarefa atual.
2. Arquivos de instrução do repositório (instruções para agentes, guias de contribuição, README, skills/receitas do projeto).
3. Configurações que o build aplica (regras de estilo, analisadores, testes de arquitetura). Elas são lei, não sugestão.
4. Este documento.
5. Suas preferências pessoais, que ficam em último lugar. Nunca as imponha sobre o padrão existente.

Se houver conflito entre fontes, siga a de maior precedência e mencione o conflito no relatório final.

## 2. Investigue antes de implementar

Antes de escrever código:

- **Leia o contexto do projeto.** Comece pelo README, pelos arquivos de instrução, pela configuração de build compartilhada, pelas regras de estilo e pelo pipeline de CI. O pipeline é a fonte de verdade de "como se valida uma mudança".
- **Mapeie a arquitetura.** Identifique as camadas ou módulos, a direção permitida das dependências e onde isso é verificado (testes de arquitetura, referências entre projetos/pacotes, regras de lint).
- **Encontre 2–3 exemplos análogos** ao que você vai fazer (um caso de uso, um endpoint, um teste parecido) e replique a forma deles: estrutura de pastas, nomes, assinatura, tratamento de erros, testes.
- **Procure antes de criar.** Verifique se já existe um utilitário, abstração, extensão, catálogo de erros, helper de teste ou chave de cache que resolva o problema. Reutilize em vez de duplicar.
- **Siga o fluxo completo** de uma operação existente, da entrada até a persistência e de volta, para entender onde entram validação, autorização, logging, cache e eventos.
- **Verifique fatos, não presuma.** Confirme nomes de símbolos, arquivos, comandos e configurações lendo o código. Não invente APIs, flags ou scripts.

## 3. Reconheça os padrões reais

- **Diferencie** o padrão predominante (repetido de forma consistente), a exceção isolada (decisão pontual, às vezes justificada em comentário) e o legado ou inconsistência (codificação de arquivos mista, redação divergente de mensagens, estilos antigos).
- **Siga o predominante** no código novo, mesmo que você prefira outra abordagem.
- **Não "normalize" exceções nem legado** fora do escopo pedido. Se a inconsistência atrapalhar a tarefa, mencione-a no relatório em vez de corrigi-la por conta própria.
- **Não idealize o design.** Se o código adota um estilo pragmático (por exemplo, modelos simples em vez de modelos ricos), mantenha esse estilo. Não reescreva para um ideal teórico.
- Ao editar um arquivo, preserve o que já está nele: codificação, quebras de linha, indentação, ordem de imports e organização.

## 4. Arquitetura e fronteiras

- **Respeite a direção das dependências.** Camadas internas (núcleo compartilhado, domínio) não conhecem as externas (aplicação, infraestrutura, apresentação). Nunca adicione uma referência que inverta essa direção, mesmo "só para um tipo".
- **Lógica de negócio fica no núcleo ou na camada de aplicação**, nunca na borda (endpoints, controllers, handlers HTTP, CLI). A borda só mapeia a entrada para um comando ou consulta, invoca o caso de uso e traduz o resultado.
- **Detalhes de persistência** (chaves, conversões, relacionamentos, nomes de tabela) ficam na infraestrutura, em configurações dedicadas, e não nas entidades.
- **Acesse recursos externos por abstrações** definidas nas camadas internas e implementadas na infraestrutura: banco, e-mail, relógio, usuário atual, tokens, hashing.
- **Não use fontes não determinísticas diretamente** na lógica de aplicação (hora atual, usuário atual, aleatoriedade). Use as abstrações existentes para manter o código testável.
- **Preocupações transversais** (validação, logging, métricas, transações) seguem o mecanismo central já adotado (decorators, middlewares, filtros, pipelines). Não espalhe essas responsabilidades pelos handlers.
- Se existirem testes de arquitetura, considere-os a definição formal das fronteiras.

## 5. Organização e nomenclatura

- **Organize por funcionalidade quando o projeto faz isso**: uma pasta por caso de uso, contendo a requisição (comando ou consulta), o handler, o validador e o DTO de resposta próprio. Se a camada de entrada espelha essa estrutura, mantenha o espelho.
- **Use nomes previsíveis e derivados do padrão existente**, por exemplo `{Verbo}{Entidade}Command`, `Get{Entidade}Query`, `{Nome}Handler`, `{Nome}Validator`, `{Nome}Response`, `{Entidade}Errors`. Confirme o padrão nos exemplos antes de usar.
- **DTOs por caso de uso** podem ser duplicados de propósito, para manter as fatias independentes. Não os unifique sem pedido.
- **Use a menor visibilidade possível.** Implementações devem ser internas ou privadas e não herdáveis quando o projeto adota isso. Exponha só contratos.
- **Prefira o registro automático existente** (varredura de assembly, convenções, auto-descoberta) ao registro manual de dependências, endpoints ou validadores.
- Siga as preferências de estilo configuradas (formato de namespace, tipos explícitos ou inferidos, chaves obrigatórias, construtores, expressões de coleção, comparações invariantes de cultura). Se o build as aplica, violá-las quebra o build.

## 6. Tratamento de erros

- **Falhas esperadas são valores, não exceções.** Use o tipo de resultado do projeto (sucesso ou falha com erro tipado). Não use exceções nem try/catch para controlar fluxo de regra de negócio.
- **Erros vêm de catálogos centralizados** por entidade ou funcionalidade, com **códigos estáveis** no formato adotado (por exemplo `"{Funcionalidade}.{Motivo}"`) e uma descrição clara.
- **Escolha o tipo semântico correto** (não encontrado, conflito, problema de regra, falha de validação, falha genérica). Ele define a resposta na borda.
- **Traduza resultados na borda** pelo mecanismo padrão (por exemplo, um helper que converte erro em resposta padronizada de problema). Não monte respostas de erro à mão em cada endpoint.
- **Exceções servem só para o inesperado**: invariantes violadas, estado impossível, falhas de infraestrutura. Um tratador global as registra e devolve uma resposta genérica.
- **Nunca exponha** stack traces, mensagens internas, consultas ou dados sensíveis ao cliente.
- Não engula erros em silêncio. Um catch sem log nem propagação é um bug.

## 7. Validação

- **Valide a forma da entrada** (obrigatoriedade, formato, limites) no mecanismo declarativo existente, executado antes do handler.
- **Os handlers aplicam regras de negócio** (existência, propriedade, estado) e não repetem a validação de formato.
- Todo novo comando que recebe entrada externa precisa de validador, seguindo o padrão dos validadores existentes.
- Erros de validação devem sair agregados e no formato padronizado já usado.

## 8. Logging e observabilidade

- **Use logging estruturado** com templates e propriedades nomeadas, nunca interpolação de string na mensagem.
- **Escolha o nível certo**: informação para o fluxo normal, aviso para situações recuperáveis, erro para falhas. Não registre a mesma falha em várias camadas.
- **Não registre dados sensíveis** (senhas, tokens, segredos, dados pessoais completos).
- **Reutilize o logging transversal existente** (decorators, middleware de contexto da requisição, telemetria). Não adicione logs redundantes em cada handler.
- Propague o contexto de correlação e rastreamento já configurado. Não crie mecanismos paralelos.

## 9. Segurança

- **Todo endpoint novo declara autorização explicitamente** (autenticação obrigatória ou permissão específica), seguindo o padrão vigente. Acesso anônimo só quando intencional e justificado.
- **Verifique a propriedade do recurso.** Operações sobre dados de um usuário filtram pelo identificador do usuário autenticado ou retornam o erro de não autorizado ou não encontrado adotado pelo projeto.
- **Trate toda entrada como não confiável.** Use consultas parametrizadas pelo mecanismo de acesso a dados e nunca concatene SQL ou comandos.
- **Segredos ficam fora do código**: configuração, variáveis de ambiente ou cofres. Não faça commit de credenciais e não as escreva em exemplos.
- **Não afrouxe controles existentes** (rate limiting, cabeçalhos de segurança, CORS, idempotência, hashing de senha, rotação de tokens, políticas de autorização) sem pedido explícito.
- Use as primitivas de segurança já presentes. Não implemente criptografia ou hashing próprios.

## 10. Performance

- **Em consultas, projete direto para o DTO** e não carregue entidades inteiras para leitura. Devolva só o necessário.
- **Pagine listagens** com o tipo e o helper de paginação existentes (busca, ordenação e página).
- **Evite N+1** e evite carregar coleções inteiras para memória antes de filtrar.
- **Use I/O assíncrono de ponta a ponta** e propague o token de cancelamento até a chamada final.
- **Cache com invalidação simétrica**: toda leitura cacheada precisa de invalidação em toda mutação que afete aqueles dados, usando as chaves centralizadas da funcionalidade.
- Não otimize prematuramente. Clareza vem primeiro, salvo quando há evidência de gargalo ou requisito explícito.

## 11. Dependências

- **Use primeiro o que já existe** no projeto (bibliotecas, abstrações, utilitários).
- **Não adicione dependências novas sem necessidade clara**, e confirme com o usuário quando for adicionar.
- Respeite o **gerenciamento centralizado de versões**, se houver. Declare versões no lugar central, não em cada projeto ou pacote.
- Não atualize versões fora do escopo. Atualizações automatizadas cuidam disso.

## 12. Simplicidade e escopo

- **Faça a menor mudança que resolve o pedido.** Cada linha alterada deve ter relação direta com a tarefa.
- **Não faça mudanças oportunistas**: refatorações, renomeações, reformatação, reorganização de imports, troca de codificação ou "melhorias" em arquivos que você não precisava tocar.
- **Não crie abstrações especulativas** (interfaces com uma única implementação sem motivo, camadas extras, configuração para casos hipotéticos).
- **Comente pouco.** Use comentários só para explicar o *porquê* (uma limitação consciente, uma decisão não óbvia), nunca para narrar o *o quê*. Siga a densidade de comentários do código ao redor.
- **Não edite à mão arquivos gerados** (migrações, snapshots, código gerado). Use a ferramenta que os gera.
- Se notar algo errado fora do escopo, **reporte, não corrija**.

## 13. Testes

- **Siga as camadas de teste existentes**: unitários para handlers e regras, testes de validador por regra, integração pela interface real (por exemplo, HTTP com dependências em containers) e testes de arquitetura para as fronteiras.
- **Imite as convenções dos testes vizinhos**: nomenclatura (por exemplo `Metodo_Should_Resultado_WhenCondicao`), estrutura Arrange/Act/Assert, helpers base, doubles e bibliotecas de asserção já usados.
- **Cubra o caminho feliz e cada caminho de falha** que o código introduz. Verifique o estado persistido e os efeitos colaterais (eventos, invalidação de cache), não só o retorno.
- **Mantenha os testes isolados**: dados únicos por teste, sem dependência de ordem.
- **Nunca enfraqueça, pule ou apague testes** para fazer a suíte passar. Corrija a causa ou reporte.

## 14. Comandos de validação

Use os comandos que o próprio projeto define. Procure-os, nesta ordem, no pipeline de CI, nos arquivos de instrução e no README. **Não invente comandos que não existam no projeto.**

| Categoria | O que fazer |
|---|---|
| Restaurar dependências | Rode o passo de restauração do pipeline antes do primeiro build. |
| Build estrito | Rode o build da solução inteira. Se avisos são tratados como erros, o build precisa terminar sem nenhum aviso. |
| Estilo e análise estática | Se regras de estilo e analisadores são aplicados durante o build, **o build é o lint**. Só use um formatador ou linter separado se o projeto definir um. |
| Testes unitários e de arquitetura | Rode a suíte de testes da solução. Esses testes não devem exigir infraestrutura externa. |
| Testes de integração | Podem exigir um runtime de containers ativo. Confirme isso antes de rodar e, se não estiver disponível, diga no relatório em vez de declarar sucesso. |
| Dependências locais | Para executar a aplicação, suba os serviços auxiliares pelo arquivo de orquestração de containers do projeto, se existir. |
| Paridade com a CI | Antes de concluir, reproduza localmente os passos da CI (restaurar → build → testar → publicar, se aplicável) na configuração usada por ela. |

Se uma ferramenta ou skill do projeto automatiza uma tarefa comum (gerar uma funcionalidade, uma migração, testes, revisar convenções), prefira usá-la a fazer manualmente.

## 15. Ambiguidades e decisões

- **Resolva primeiro pela codebase**: o padrão existente costuma responder a dúvida.
- **Pergunte ao usuário** só quando a decisão for dele (regra de negócio, contrato público, trade-off sem padrão estabelecido) e a resposta mudar o que você vai fazer.
- Quando seguir sem perguntar, **escolha a opção mais conservadora** e coerente com o código existente, e **registre a suposição** no relatório.
- **Não faça ações destrutivas ou externas sem confirmação**: apagar dados ou arquivos, reescrever histórico, publicar, fazer push. **Commit só quando pedido explicitamente.**
- Antes de sobrescrever ou apagar algo, examine o conteúdo atual.

## 16. Checklist antes de concluir

- [ ] A mudança atende exatamente ao pedido, sem extras fora do escopo.
- [ ] As fronteiras de arquitetura e a direção das dependências foram respeitadas.
- [ ] Nomes, estrutura, tratamento de erros, validação, logging e testes seguem os exemplos análogos.
- [ ] Autorização, verificação de propriedade e invalidação de cache foram consideradas quando aplicáveis.
- [ ] O build passa sem avisos, nas mesmas condições da CI.
- [ ] Os testes relevantes passam, e os novos cobrem o caminho feliz e as falhas.
- [ ] O diff foi revisado linha a linha: nenhuma reformatação, arquivo estranho ou artefato acidental.
- [ ] O relatório final diz com honestidade o que foi feito, o que foi verificado (com o resultado) e o que **não** foi verificado ou ficou pendente.
