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

- **Ocelot API Gateway**: Proxy implementat folosind Ocelot
- **Load Balancing**: Algoritm Round-Robin automat între multiple instanțe DW
- **Caching**: Cache în-memory pentru răspunsuri (configurabil)
- **Circuit Breaker**: Protecție automată împotriva backend-urilor care eșuează
- **Quality of Service**: Timeout și retry logic automat

## Structura Proiectului

```
SmartProxy/
├── Api/                    # Data Warehouse (Etapa 1)
│   ├── Controllers/       # BooksController, HealthController
│   ├── Models/            # Book, BookCreateRequest, BookUpdateRequest
│   ├── Services/          # CassandraService
│   └── Program.cs
├── Proxy/                  # Reverse Proxy (Etapa 2) - Ocelot
│   ├── Controllers/       # HealthController, CacheController
│   ├── Services/           # CacheService (pentru inspecție)
│   ├── ocelot.json        # Configurare Ocelot
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
- **API 3**: http://localhost:5002
- **Cassandra Node 1**: localhost:9042
- **Cassandra Node 2**: localhost:9043
- **Redis**: localhost:6379

### Rulare locală (dezvoltare)

1. **Pornește Cassandra și Redis:**
   ```bash
   docker-compose up -d cassandra cassandra2 redis
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

## Testare

### Scripturi de Testare

**Linux/Mac/WSL:**
```bash
chmod +x scripts/test-all.sh
./scripts/test-all.sh
```

**Windows (PowerShell):**
```powershell
.\scripts\test-all.ps1
```

Aceste scripturi testează automat:
- Health checks pentru toate serviciile
- CRUD operations
- Load balancing
- Caching
- Cluster Cassandra

Vezi `TESTING-GUIDE.md` pentru detalii complete despre testare.

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

### Ocelot API Gateway

Proxy-ul este implementat folosind **Ocelot**, un API Gateway pentru .NET care oferă:

### Load Balancing (Round-Robin)

Ocelot distribuie automat cererile între 3 instanțe DW (api1, api2, api3) folosind algoritmul Round-Robin. Configurarea se face în `ocelot.json`.

### Caching

- **Cache în-memory**: Răspunsurile sunt cache-uite automat pentru rutele configurate
- **TTL configurabil**: Cache-ul expiră după 5 minute (configurabil în `ocelot.json`)
- **Regiuni cache**: Cache-ul poate fi organizat pe regiuni pentru invalidare selectivă

### Circuit Breaker

- **Protecție automată**: Dacă un backend eșuează de 3 ori, Ocelot oprește temporar cererile către acel backend
- **Recuperare automată**: După 1 secundă, încearcă din nou

### Quality of Service

- **Timeout**: 5 secunde pentru fiecare cerere
- **Retry logic**: Gestionare automată a erorilor temporare

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

#### Proxy (Ocelot)

Configurarea se face în `ocelot.json`:

```json
{
  "Routes": [
    {
      "DownstreamHostAndPorts": [
        { "Host": "api1", "Port": 8080 },
        { "Host": "api2", "Port": 8080 }
      ],
      "LoadBalancerOptions": {
        "Type": "RoundRobin"
      },
      "FileCacheOptions": {
        "TtlSeconds": 300
      }
    }
  ]
}
```

Pentru inspecția cache-ului (opțional în `appsettings.json`):
```json
{
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

Ocelot cache-uiește automat răspunsurile pentru GET requests. Pentru a verifica:

**Metoda 1: Măsurarea timpului**
```bash
# Prima cerere - cache MISS (contactează backend, ~50-200ms)
time curl http://localhost:8080/books

# A doua cerere - cache HIT (din cache, ~1-10ms)
time curl http://localhost:8080/books
```

**Metoda 2: Verificare loguri**
```bash
# Vezi logurile proxy-ului pentru cache status
docker-compose logs -f proxy | grep -i cache
```

**Metoda 3: Test complet**
```bash
# Face 5 cereri și observă diferența de timp
for i in {1..5}; do
  echo "Request $i:"
  time curl -s http://localhost:8080/books > /dev/null
done
```

**Indicatori cache funcțional:**
- ✅ Prima cerere: mai lentă (contactează backend)
- ✅ Cererile 2-5: mult mai rapide (< 10ms)
- ✅ Logurile arată cache HIT pentru cereri repetate

Vezi `CACHE-TESTING.md` pentru detalii complete.

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

