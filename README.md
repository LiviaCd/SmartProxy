# SmartProxy - Web Proxy cu Load Balancing și Caching

Proiect realizat pentru Lucrarea de Laborator 2 - Web proxy: realizarea transparenței în distribuire.

## Arhitectură

Proiectul constă din două componente principale:

1. **Api (Data Warehouse - DW)**: Server informativ care stochează date în Cassandra și oferă API REST
2. **Proxy (Reverse Proxy)**: Intermediar între clienți și serverele DW, cu funcționalități de caching și load balancing

## Componente

### Etapa 1: Data Warehouse (Api)

- **Comunicare concurentă**: ASP.NET Core procesează automat cererile concurent prin thread pool
- **Protocol HTTP**: Suport pentru GET, POST, PUT, DELETE
- **Format negotiation**: Suport pentru JSON și XML (prin Accept header)
- **Stocare date**: Cassandra pentru scalabilitate distribuită
- **Thread-safe**: Colecțiile și operațiunile sunt thread-safe prin design-ul ASP.NET Core

### Etapa 2: Reverse Proxy (Proxy)

- **Smart Proxy**: Păstrează conexiunile și forward-ează cererile către backend
- **Caching**: Memorare temporară a răspunsurilor în Redis pentru GET requests
- **Load Balancing**: Algoritm Round-Robin pentru distribuirea cererilor între multiple instanțe DW
- **Redis**: Sistem rapid de stocare key-value pentru cache

## Structura Proiectului

```
SmartProxy/
├── Api/                    # Data Warehouse (Etapa 1)
│   ├── Controllers/       # BooksController, HealthController
│   ├── Models/            # Book, BookCreateRequest, BookUpdateRequest
│   ├── Services/          # CassandraService
│   └── Program.cs
├── Proxy/                  # Reverse Proxy (Etapa 2)
│   ├── Controllers/       # HealthController
│   ├── Middleware/         # ReverseProxyMiddleware
│   ├── Services/           # LoadBalancerService, CacheService
│   └── Program.cs
├── cassandra-init/         # Scripturi de inițializare Cassandra
├── docker-compose.yml      # Configurație Docker pentru toate serviciile
└── README.md
```

## Tehnologii

- **.NET 8.0**: Framework pentru API și Proxy
- **Cassandra 4.1**: Baza de date NoSQL distribuită
- **Redis 7**: Cache distribuit pentru răspunsuri
- **Docker & Docker Compose**: Containerizare și orchestrere

## Instalare și Rulare

### Prerechizite

- Docker și Docker Compose
- .NET 8.0 SDK (pentru dezvoltare locală)

### Rulare cu Docker Compose

1. **Pornește toate serviciile:**
   ```bash
   docker-compose up -d
   ```

2. **Verifică statusul:**
   ```bash
   docker-compose ps
   ```

3. **Vezi logurile:**
   ```bash
   docker-compose logs -f
   ```

### Servicii disponibile

- **Proxy**: http://localhost:8080
- **API 1**: http://localhost:5000
- **API 2**: http://localhost:5001
- **Cassandra**: localhost:9042
- **Redis**: localhost:6379

### Rulare locală (dezvoltare)

1. **Pornește Cassandra și Redis:**
   ```bash
   docker-compose up -d cassandra redis
   ```

2. **Rulează Api:**
   ```bash
   cd Api
   dotnet run
   ```

3. **Rulează Proxy:**
   ```bash
   cd Proxy
   dotnet run
   ```

## Utilizare API

### Format Negotiation (JSON/XML)

API-ul suportă atât JSON cât și XML prin header-ul `Accept`:

```bash
# JSON (default)
curl -H "Accept: application/json" http://localhost:5000/books

# XML
curl -H "Accept: application/xml" http://localhost:5000/books
```

### Endpoints

#### Books API (direct la DW)

- `GET /books` - Listă toate cărțile
- `GET /books/{id}` - Obține o carte după ID
- `POST /books` - Creează o carte nouă
- `PUT /books/{id}` - Actualizează o carte
- `DELETE /books/{id}` - Șterge o carte

#### Health Check

- `GET /health` - Verifică statusul serviciului

### Utilizare prin Proxy

Toate cererile către API trebuie să treacă prin proxy:

```bash
# Prin proxy (cu load balancing și caching)
curl http://localhost:8080/books

# Creare carte prin proxy
curl -X POST http://localhost:8080/books \
  -H "Content-Type: application/json" \
  -d '{"title":"Test Book","author":"Test Author","year":2024}'
```

## Funcționalități

### Load Balancing (Round-Robin)

Proxy-ul distribuie cererile între multiple instanțe DW folosind algoritmul Round-Robin. Fiecare cerere este direcționată către următorul server disponibil în rotație.

### Caching

- **Cache pentru GET requests**: Răspunsurile la cererile GET sunt cache-uite în Redis
- **Invalidare automată**: La operațiuni de scriere (POST, PUT, DELETE), cache-ul este invalidat automat
- **Expirare**: Cache-ul expiră după 5 minute (configurabil)

### Smart Proxy

- **Păstrare conexiuni**: Proxy-ul menține conexiuni persistente către backend
- **Forwarding complet**: Headerele și body-ul sunt forward-ate complet
- **Error handling**: Gestionare erori și retry logic

## Configurare

### appsettings.json

#### Api
```json
{
  "Cassandra": {
    "Hosts": "127.0.0.1",
    "Keyspace": "techframer",
    "LocalDatacenter": "datacenter1"
  }
}
```

#### Proxy
```json
{
  "Proxy": {
    "BackendServers": "http://api1:8080,http://api2:8080",
    "EnableCaching": true,
    "CacheExpirationSeconds": 300
  },
  "Redis": {
    "ConnectionString": "redis:6379"
  }
}
```

## Vizualizare Date Redis

### Opțiunea 1: API Endpoints

**Vizualizează toate cheile din cache:**
```bash
curl http://localhost:8080/api/cache/keys
```

**Vizualizează o cheie specifică:**
```bash
curl http://localhost:8080/api/cache/keys/GET:/books
```

**Statistici cache:**
```bash
curl http://localhost:8080/api/cache/stats
```

**Șterge o cheie:**
```bash
curl -X DELETE http://localhost:8080/api/cache/keys/GET:/books
```

**Șterge tot cache-ul:**
```bash
curl -X DELETE http://localhost:8080/api/cache/keys
```

### Opțiunea 2: Redis CLI

```bash
# Conectează-te la Redis
docker exec -it smartproxy-redis redis-cli

# Vezi toate cheile
KEYS *

# Vezi o cheie specifică
GET "GET:/books"

# Vezi TTL (Time To Live)
TTL "GET:/books"

# Șterge o cheie
DEL "GET:/books"

# Șterge toate cheile
FLUSHALL
```

## Testare

### Test Load Balancing

```bash
# Faceți mai multe cereri și observați distribuția
for i in {1..10}; do
  curl http://localhost:8080/books
  echo ""
done
```

### Test Caching

```bash
# Prima cerere - cache miss
time curl http://localhost:8080/books

# A doua cerere - cache hit (mai rapidă)
time curl http://localhost:8080/books
```

### Test Format Negotiation

```bash
# JSON
curl -H "Accept: application/json" http://localhost:5000/books

# XML
curl -H "Accept: application/xml" http://localhost:5000/books
```

## Persistența Datelor

### Configurație Persistență

Proiectul este configurat pentru persistență completă a datelor:

1. **Cassandra**: 
   - Datele sunt stocate în volume Docker (`cassandra-data`)
   - Persistență automată la fiecare scriere
   - Datele rămân disponibile după restart

2. **Redis**:
   - AOF (Append Only File) activat cu `appendfsync always`
   - Fiecare scriere este sincronizată imediat pe disc
   - Datele cache sunt persistente între restarts

### Backup și Restore

#### Backup Manual

**Linux/Mac/WSL:**
```bash
# Backup Redis
chmod +x scripts/backup-redis.sh
./scripts/backup-redis.sh

# Backup Cassandra
chmod +x scripts/backup-cassandra.sh
./scripts/backup-cassandra.sh

# Backup Complet (Redis + Cassandra)
chmod +x scripts/backup-all.sh
./scripts/backup-all.sh
```

**Windows (PowerShell):**
```powershell
# Backup Redis
.\scripts\backup-redis.ps1

# Backup Cassandra
.\scripts\backup-cassandra.ps1

# Backup Complet (Redis + Cassandra)
.\scripts\backup-all.ps1
```

#### Restore

**Linux/Mac/WSL:**
```bash
# Restore Redis
chmod +x scripts/restore-redis.sh
./scripts/restore-redis.sh redis-backups/redis_backup_YYYYMMDD_HHMMSS.rdb

# Restore Cassandra
chmod +x scripts/restore-cassandra.sh
./scripts/restore-cassandra.sh cassandra-backups/cassandra_backup_YYYYMMDD_HHMMSS
```

**Windows:**
```powershell
# Restore Redis (folosește Git Bash sau WSL pentru scripturile bash)
# Sau copiază manual fișierul RDB în container:
docker cp redis-backups\redis_backup_YYYYMMDD_HHMMSS.rdb smartproxy-redis:/data/dump.rdb
docker restart smartproxy-redis
```

#### Backup Automat

**Linux/Mac (Cron):**
```bash
# Backup zilnic la 2:00 AM
0 2 * * * cd /path/to/SmartProxy && ./scripts/backup-all.sh >> logs/backup.log 2>&1
```

**Windows (Task Scheduler):**
1. Deschide Task Scheduler
2. Creează task nou care rulează zilnic la 2:00 AM
3. Acțiune: `powershell.exe -File "D:\path\to\SmartProxy\scripts\backup-all.ps1"`

### Verificare Persistență

**Test Redis:**
```bash
# Adaugă date
docker exec -it smartproxy-redis redis-cli SET test "persistent data"

# Restart Redis
docker restart smartproxy-redis

# Verifică datele (ar trebui să existe)
docker exec -it smartproxy-redis redis-cli GET test
```

**Test Cassandra:**
```bash
# Adaugă date prin API
curl -X POST http://localhost:5000/books \
  -H "Content-Type: application/json" \
  -d '{"title":"Test","author":"Author","year":2024}'

# Restart Cassandra
docker restart smartproxy-cassandra

# Verifică datele (ar trebui să existe)
curl http://localhost:5000/books
```

## Oprire

```bash
# Oprește toate serviciile (păstrează datele)
docker-compose down

# Oprește și șterge volume-urile (ȘTERGE TOATE DATELE!)
docker-compose down -v
```

## Structura Bazei de Date

### Keyspace: techframer

### Tabel: books

```cql
CREATE TABLE books (
    id UUID PRIMARY KEY,
    title TEXT,
    author TEXT,
    year INT
);
```

## Referințe

- [Scalable Web Architecture and Distributed Systems](http://aosabook.org/en/distsys.html)
- [Forward Proxy vs Reverse Proxy](http://www.jscape.com/blog/bid/87783/Forward-Proxy-vsReverse-Proxy)
- [Smart Proxy Pattern](http://www.eaipatterns.com/SmartProxy.html)
- [HTTP Caching](http://www.w3.org/Protocols/rfc2616/rfc2616-sec13.html)

