# Validação da implementação

Validação executada em 18/09/2026 no ambiente local com .NET SDK 8.0.131 e Docker.

## Resultados

| Verificação | Resultado |
|---|---|
| Compilação .NET | Sucesso, sem erros ou avisos |
| Build das três imagens Docker | Sucesso |
| `dotnet test --no-restore` | 14 testes aprovados |
| `python3 scripts/smoke.py --resilience` | 25 verificações aprovadas |
| `python3 scripts/check_operations.py` | 5 verificações aprovadas |
| `dotnet list package --vulnerable --include-transitive` | Nenhuma vulnerabilidade conhecida indicada pelas fontes consultadas naquele momento |

## Cobertura exercitada

- Persistência relacional com migrations e bancos temporários nos testes C#.
- Validação de preços, quantidades, pedido vazio e item nulo.
- Reserva de todos os itens ou rollback completo.
- Disputa concorrente pela última unidade.
- Reserva e liberação idempotentes.
- Evento duplicado e evento incompatível com a reserva.
- Cálculo do total, agrupamento de itens e preservação do preço.
- Confirmação do pedido com registro na outbox.
- Recuperação de pedido pendente.
- Fluxo real via Gateway, RabbitMQ e bancos nos containers.
- Tokens ausentes, inválidos e expirados; senha incorreta; permissão por perfil.
- Isolamento de pedidos entre usuários.
- Proteção das APIs mesmo quando acessadas diretamente pela rede interna.
- JWT administrativo recusado nas operações restritas à credencial interna.
- Reenvio pela outbox depois de reiniciar Vendas durante indisponibilidade do RabbitMQ.
- Retomada de pedidos depois de reiniciar Vendas e restaurar o Estoque.
- Persistência de dados após reiniciar os microsserviços.
- Evento JSON inválido preservado na fila de erros após falhas de processamento.

## Observações do ambiente

O build padrão no Docker encontrou erro NU1301 ao acessar o NuGet. A compilação foi concluída usando a rede do host apenas na etapa de build, com a mesma configuração disponibilizada em `compose.build-host.yaml`. A execução dos serviços foi validada na rede interna normal do Compose.

Os testes criaram produtos e pedidos de demonstração. O teste operacional deixou um evento inválido identificado com prefixo `teste-dlq-` na fila de erros. Nenhum desses dados é uma informação de produção.

## Limites das evidências

Não foram realizados testes de carga, auditoria de segurança completa, implantação pública ou testes com várias réplicas. A ausência de vulnerabilidades conhecidas na consulta de pacotes não substitui uma revisão de segurança.

A validação cobre o comportamento da implementação local e os cenários descritos. As limitações funcionais e arquiteturais estão registradas no README e no guia de entrevista.
