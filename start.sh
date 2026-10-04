#!/usr/bin/env bash
set -e

echo "=================================================="
echo "🚀 Iniciando Ambiente MAF Workflow & Sales Admin"
echo "=================================================="

# 1. Iniciar Banco PostgreSQL pgvector
echo "[1/4] Verificando/Iniciando Banco de Dados PostgreSQL (maf-sales-db)..."
if docker ps -a --format '{{.Names}}' | grep -q "^maf-sales-db$"; then
    docker start maf-sales-db >/dev/null 2>&1 || true
else
    if [ -f "sales-workflow/infra/docker-compose.yml" ]; then
        (cd sales-workflow/infra && docker compose up -d)
    fi
fi

# 2. Garantir estrutura de infra no BFF para o Banco NoSQL
echo "[2/4] Verificando diretório de infraestrutura no BFF..."
mkdir -p agent-workflow-bff/infra/data

# 3. Informar localização do Banco NoSQL
echo "[3/4] Banco NoSQL do BFF configurado em: agent-workflow-bff/infra/data/bff_nosql_db.json"

# 4. Status dos Serviços
echo "[4/4] Tudo pronto! Os serviços podem ser iniciados com os seguintes comandos:"
echo "  • Admin API (PostgreSQL): dotnet run --project sales-workflow/admin/backend/SalesAdmin.Api"
echo "  • Agent BFF (NoSQL Store): dotnet run --project agent-workflow-bff"
echo "  • Sales Worker:           dotnet run --project sales-workflow/infra/SalesWorkflow.csproj"
echo "=================================================="
