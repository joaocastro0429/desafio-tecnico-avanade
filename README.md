# Desafio Avanade — Estoque e Vendas

Backend em C#/.NET 8 com dois microsserviços, Entity Framework Core, SQLite, RabbitMQ, JWT e API Gateway YARP. Implementação orientada pelo [PRD](PRD.md).

Para estudar o código e se preparar para a entrevista, comece pelo [guia da implementação](docs/IMPLEMENTACAO_E_ENTREVISTA.md).

## Executar com Docker

Pré-requisitos: Docker com Compose e Python 3. Execute na raiz do projeto:

```bash
python3 scripts/setup.py
docker compose up --build -d
python3 scripts/smoke.py
```

A primeira execução baixa imagens e dependências e pode levar alguns minutos. O script de teste aguarda o Gateway iniciar e cria dados de demonstração.

- API: `http://localhost:5000`.
- Saúde do Gateway: `http://localhost:5000/health`.
- Painel RabbitMQ: `http://localhost:15672`.
- Os microsserviços e a porta AMQP ficam na rede interna do Compose.

O script `setup.py` gera o arquivo `.env` com segredos aleatórios, não o sobrescreve e não imprime credenciais. Abra esse arquivo localmente para consultar as senhas:

| Usuário | Senha no `.env` | Perfil |
|---|---|---|
| `admin` | `DEMO_ADMIN_PASSWORD` | Administrador |
| `cliente` | `DEMO_CLIENT_PASSWORD` | Cliente |
| `cliente2` | `DEMO_CLIENT2_PASSWORD` | Cliente |

O RabbitMQ usa `RABBIT_USER` e `RABBIT_PASSWORD`. Não versione nem envie o `.env` ao recrutador. Ele pode gerar suas próprias credenciais. Os hashes são enviados ao Gateway; as senhas de demonstração em texto ficam apenas no arquivo local para facilitar o uso.

```bash
docker compose ps
docker compose logs -f estoque vendas gateway
docker compose stop
docker compose start
```

Os dados ficam em volumes separados e sobrevivem ao reinício. `docker compose down` remove containers e rede, preservando volumes. Evite a opção `-v` se quiser manter os dados. Não gere outro `.env` para um volume RabbitMQ já inicializado sem também administrar as credenciais do broker.

### Falha de acesso ao NuGet durante o build

Se ocorrer `NU1301` apenas dentro do Docker no Linux, mas o host conseguir acessar o NuGet, tente:

```bash
docker compose -f compose.yaml -f compose.build-host.yaml build
docker compose up -d
```

O arquivo adicional usa a rede do host apenas durante a compilação; a execução continua na rede interna padrão. É uma alternativa para ambientes Linux com problemas de rede no build, não um requisito da aplicação.

## Usar a API

Consulte [exemplos.http](docs/exemplos.http) para a sequência de requisições. Também é possível usar Postman, Insomnia ou curl.

1. Faça `POST /auth/login` com `username` e `password`.
2. Copie `accessToken` da resposta.
3. Envie `Authorization: Bearer SEU_TOKEN` nas rotas protegidas.
4. Com o administrador, cadastre um produto.
5. Com um cliente, crie um pedido usando o ID retornado.
6. Consulte o pedido e o estoque.

| Método | Rota | Permissão |
|---|---|---|
| POST | `/auth/login` | Pública, com limite de tentativas |
| GET | `/health` | Pública; verifica que o processo responde |
| POST | `/produtos` | Administrador |
| GET | `/produtos` | Usuário autenticado |
| GET | `/produtos/{id}` | Usuário autenticado |
| PATCH | `/produtos/{id}/estoque` | Administrador; define quantidade física |
| POST | `/pedidos` | Usuário autenticado |
| GET | `/pedidos` | Somente os pedidos do usuário |
| GET | `/pedidos/{id}` | Somente o proprietário |

`POST /pedidos` retorna:

- `201`: confirmado, com estoque reservado e evento persistido na outbox.
- `202`: pendente porque o Estoque não pôde ser consultado; consulte a URL do cabeçalho `Location`.
- `409`: pedido rejeitado por indisponibilidade ou produto inexistente, com ID e motivo no corpo.
- `400`: estrutura, quantidade ou itens inválidos; nenhum pedido é criado.

Um pedido confirmado pode aguardar a baixa física enquanto o evento é processado. Nesse intervalo, `reservada` aumenta e `disponivel` já diminui. Depois da mensagem, `quantidade` diminui e `reservada` é liberada.

A consulta de pedido de outro usuário retorna `404` para não revelar sua existência. Outras respostas relevantes: `401` para token ausente/inválido/expirado e `403` para falta de permissão.

## Compilar e testar

Para trabalhar no código, instale o SDK .NET 8. O `global.json` aceita as faixas de SDK 8.0 compatíveis.

```bash
dotnet restore
dotnet build
dotnet test
```

Os testes C# usam bancos SQLite temporários reais; não dependem do RabbitMQ nem do Docker. Exercitam reservas, concorrência, rollback, duplicação de eventos, cálculo de pedidos, outbox e recuperação de pendências.

Com o Compose em execução:

```bash
python3 scripts/smoke.py
python3 scripts/smoke.py --resilience
python3 scripts/check_operations.py
```

O segundo comando também para e reinicia temporariamente **os containers deste projeto** para verificar recuperação do broker, persistência da outbox e retomada de pedidos. Execute sem outras operações em andamento. Os testes de fluxo preservam os dados criados; não são testes de carga. `check_operations.py` verifica autenticação diretamente nas APIs pela rede interna e publica um evento inválido proposital, que permanece na fila de erros para inspeção.

## Migrations

Cada API aplica suas migrations na inicialização, adequado à demonstração com uma instância de cada serviço. Para criar uma migration após mudar um modelo:

```bash
dotnet tool restore
dotnet ef migrations add NomeDaMudanca --project src/Estoque.Api --output-dir Data/Migrations
# Ou use --project src/Vendas.Api para o banco de Vendas.
```

As factories de design permitem gerar migrations sem iniciar os serviços. O EF pode informar que não carregou a configuração JWT do host e continuar usando a factory; isso não impede a geração. Revise as migrations antes de usá-las. Em produção, aplicá-las seria uma etapa controlada de implantação.

## Estrutura

```text
src/
  Estoque.Api/   # Produtos, reservas e consumo de eventos
  Vendas.Api/    # Pedidos, recuperação e outbox
  Gateway/       # Login de demonstração, JWT e roteamento
  Shared/        # Contratos, segurança e topologia RabbitMQ
tests/
  Desafio.Tests/ # Testes de regras e persistência
scripts/
  setup.py      # Configuração local sem senhas fixas
  smoke.py      # Testes das aplicações reais
  check_operations.py # Proteção interna e fila de erros
docs/
  IMPLEMENTACAO_E_ENTREVISTA.md
  exemplos.http
```

As entidades e os bancos não são compartilhados. `Shared` contém apenas contratos e infraestrutura comuns para reduzir repetição neste desafio.

## Limites da entrega

- Aplicação de demonstração local, sem interface visual, pagamento ou envio.
- Uma instância de cada API; SQLite e a coordenação de pedidos em memória não constituem uma solução de escala horizontal.
- Usuários fixos com hashes configurados, sem cadastro, refresh token ou revogação de sessão.
- HTTP local e chaves simétricas compartilhadas para simplificar o ambiente; produção requer transporte seguro e gestão de identidade/segredos apropriada.
- Listagens sem paginação, consumidor com polling e logs no console; `/health` não verifica dependências.
- Pedidos pendentes são recuperados tentando concluir a venda. Não há cancelamento público nem expiração automática de reservas.
- Mensagens com falha após três tentativas vão para a fila `estoque.venda-confirmada.erros.v1`. Correção e reprocessamento são manuais; não há painel de operações próprio.
- O cliente não deve reenviar cegamente `POST /pedidos`: cada requisição cria um novo pedido. Uma futura chave de idempotência protegeria também essa entrada.

Veja detalhes e sugestões de evolução no guia de entrevista e os [resultados da validação](docs/VALIDACAO.md).
