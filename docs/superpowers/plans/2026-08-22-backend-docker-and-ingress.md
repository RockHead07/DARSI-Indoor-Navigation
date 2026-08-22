# Backend Dockerization & Ingress Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Containerize the DARSI backend with Docker Compose (FastAPI + PostgreSQL pgvector) and provide automated Ingress setup (Cloudflare Tunnel) for private server hosting per ADR-027.

**Architecture:** Docker Compose orchestrates a PostgreSQL container (`pgvector/pgvector:pg16`) with automatic schema initialization (`schema.sql` & `schema_rag.sql`), and a FastAPI application container with persistent FastEmbed ONNX weights caching. Ingress is managed via Cloudflare Tunnel (`cloudflared`) to expose the private server over public HTTPS without opening inbound firewall ports or requiring client VPNs.

**Tech Stack:** Docker, Docker Compose, PostgreSQL 16 + pgvector (`pgvector/pgvector:pg16`), Python 3.11/3.12, FastAPI, FastEmbed (`paraphrase-multilingual-MiniLM-L12-v2`), Cloudflare Tunnel (`cloudflared`).

## Global Constraints

- Database connection strictly via standard `DATABASE_URL` connection string (ADR-001 / ADR-014 / ADR-027)
- PostgreSQL must have `vector` extension enabled (`pgvector/pgvector:pg16` base image)
- Model cache directory must persist across container restarts to avoid re-downloading ~500MB weights on startup
- Cloudflare Tunnel ingress must not require inbound port forwarding or client-side VPN (ADR-027)
- Unity AssistantClient must be able to communicate with the HTTPS endpoint out of the box

---

### Task 1: Create `Dockerfile` in `darsi-backend`

**Files:**
- Create: `D:/Dev/Projects/darsi-backend/Dockerfile`
- Reference: `D:/Dev/Projects/darsi-backend/requirements.txt`
- Reference: `D:/Dev/Projects/darsi-backend/app/main.py`

**Interfaces:**
- Consumes: Python 3.11-slim base image, `requirements.txt`
- Produces: Runnable container image exposing FastAPI on port 8000

- [ ] **Step 1: Write `Dockerfile`**

```dockerfile
FROM python:3.11-slim

# Set environment variables
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    FASTEMBED_CACHE_PATH=/app/.fastembed_cache

WORKDIR /app

# Install system dependencies needed for building packages / healthchecks
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Install python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application source code
COPY . .

# Create cache directory for FastEmbed weights
RUN mkdir -p /app/.fastembed_cache

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
```

- [ ] **Step 2: Verify Dockerfile creation and requirements match**

---

### Task 2: Create `docker-compose.yml` and `.env.docker.example`

**Files:**
- Create: `D:/Dev/Projects/darsi-backend/docker-compose.yml`
- Create: `D:/Dev/Projects/darsi-backend/.env.docker.example`
- Reference: `D:/Dev/Projects/darsi-backend/schema.sql`
- Reference: `D:/Dev/Projects/darsi-backend/schema_rag.sql`

**Interfaces:**
- Consumes: `schema.sql`, `schema_rag.sql`, `Dockerfile`
- Produces: Multi-container setup (`db` with pgvector + `api` with FastAPI)

- [ ] **Step 1: Write `docker-compose.yml`**

```yaml
version: '3.8'

services:
  db:
    image: pgvector/pgvector:pg16
    container_name: darsi-db
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-postgres}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-darsi}
      POSTGRES_DB: ${POSTGRES_DB:-darsi}
    ports:
      - "${DB_PORT:-5433}:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./schema.sql:/docker-entrypoint-initdb.d/01_schema.sql:ro
      - ./schema_rag.sql:/docker-entrypoint-initdb.d/02_schema_rag.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-postgres} -d ${POSTGRES_DB:-darsi}"]
      interval: 5s
      timeout: 5s
      retries: 5

  api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: darsi-api
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
    environment:
      DATABASE_URL: postgresql://${POSTGRES_USER:-postgres}:${POSTGRES_PASSWORD:-darsi}@db:5432/${POSTGRES_DB:-darsi}
      POI_SYNC_TOKEN: ${POI_SYNC_TOKEN:-darsi-admin-token}
      GROQ_API_KEY: ${GROQ_API_KEY}
      CORS_ORIGINS: ${CORS_ORIGINS:-*}
    ports:
      - "${API_PORT:-8000}:8000"
    volumes:
      - fastembed_cache:/app/.fastembed_cache

volumes:
  pgdata:
  fastembed_cache:
```

- [ ] **Step 2: Write `.env.docker.example`**

```env
# ==========================================
# DARSI Backend Docker Compose Configuration
# ==========================================

# Postgres Database Settings
POSTGRES_USER=postgres
POSTGRES_PASSWORD=darsi
POSTGRES_DB=darsi
DB_PORT=5433

# API Settings
API_PORT=8000
POI_SYNC_TOKEN=darsi-admin-token
CORS_ORIGINS=*

# Groq API Key (wajib untuk RAG assistant)
GROQ_API_KEY=your_groq_api_key_here
```

---

### Task 3: Ingestion & In-Container Helper Scripts

**Files:**
- Create: `D:/Dev/Projects/darsi-backend/scripts/docker_seed.py`
- Modify: `D:/Dev/Projects/darsi-backend/README.md`

**Interfaces:**
- Consumes: `data/corpus_simulasi.py`, `app.assistant.embedding`
- Produces: CLI helper to ingest corpus and run evaluation directly inside Docker container

- [ ] **Step 1: Write `docker_seed.py`**
- [ ] **Step 2: Update README with quickstart commands**

---

### Task 4: Cloudflare Tunnel Guide & Setup Automation

**Files:**
- Create: `D:/Dev/Projects/darsi-backend/docs/TUNNEL-SETUP.md`
- Create: `D:/Dev/Projects/darsi-backend/scripts/setup_cloudflare_tunnel.sh`

**Interfaces:**
- Consumes: Server CLI / Linux shell
- Produces: Step-by-step setup and automated daemon installation for Zero Trust Cloudflare Tunnel

- [ ] **Step 1: Write `scripts/setup_cloudflare_tunnel.sh`**
- [ ] **Step 2: Write `docs/TUNNEL-SETUP.md`**

---

### Task 5: Verification & End-to-End Connectivity Check

**Files:**
- Review: `D:/Dev/Projects/darsi-backend/docker-compose.yml`
- Review: `D:/Dev/Projects/darsi-backend/Dockerfile`
- Review: `D:/Dev/Projects/UnityProjects/Learning/DARSI-Indoor Navigation/Assets/Scripts/AssistantClient.cs`

- [ ] **Step 1: Verify syntax and schema bindings**
- [ ] **Step 2: Update README with complete run instructions**
