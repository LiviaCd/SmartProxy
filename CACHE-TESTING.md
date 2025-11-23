# Verificare Cache în Ocelot

## Metode de Verificare

### 1. Test Manual - Măsurarea Timpului de Răspuns

Cea mai simplă metodă este să măsori timpul de răspuns pentru cereri repetate:

```bash
# Prima cerere - cache MISS (va contacta backend)
time curl -v http://localhost:8080/books

# A doua cerere - cache HIT (ar trebui să fie mult mai rapidă)
time curl -v http://localhost:8080/books
```

**Indicatori de cache HIT:**
- Timp de răspuns mult mai scurt (< 10ms vs > 50ms)
- Nu vezi loguri de forward către backend în a doua cerere

### 2. Verificare Loguri

Ocelot loghează automat cache hits/misses. Verifică logurile:

```bash
# Vezi logurile proxy-ului
docker-compose logs -f proxy | grep -i cache

# Sau pentru toate logurile
docker-compose logs -f proxy
```

În loguri vei vedea:
- `CacheLoggingHandler` va loga cache status pentru fiecare cerere
- Ocelot va loga automat cache operations

### 3. Test cu Script

```bash
# Test cache cu multiple cereri
for i in {1..5}; do
  echo "Request $i:"
  time curl -s http://localhost:8080/books > /dev/null
  echo ""
done
```

**Rezultat așteptat:**
- Request 1: ~50-200ms (cache MISS - contactează backend)
- Request 2-5: ~1-10ms (cache HIT - din cache)

### 4. Verificare Headere HTTP

Ocelot poate adăuga header-e care indică cache status. Verifică răspunsurile:

```bash
# Verifică headerele răspunsului
curl -I http://localhost:8080/books

# Sau cu verbose
curl -v http://localhost:8080/books 2>&1 | grep -i cache
```

### 5. Test Cache Expiration

```bash
# Face o cerere
curl http://localhost:8080/books

# Așteaptă peste 5 minute (300 secunde - TTL-ul cache-ului)
# Sau modifică TTL în ocelot.json la 10 secunde pentru test rapid

# Face o altă cerere după expirare
curl http://localhost:8080/books
# Ar trebui să fie cache MISS (contactează din nou backend)
```

## Configurare Logging Detaliat

Pentru logging mai detaliat, actualizează `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Ocelot": "Debug",
      "Proxy.DelegatingHandlers": "Information"
    }
  }
}
```

## Verificare Cache în Cod

Cache-ul Ocelot este în-memory și nu este direct accesibil prin API. Pentru a verifica:

1. **Măsurarea timpului** - cea mai simplă metodă
2. **Logging** - vezi logurile pentru cache operations
3. **Monitoring** - folosește Application Insights sau alte tool-uri

## Test Complet

```bash
# 1. Curăță cache-ul (restart proxy)
docker-compose restart proxy

# 2. Prima cerere (cache MISS)
echo "=== Request 1 (Cache MISS) ==="
time curl -s http://localhost:8080/books > /dev/null

# 3. A doua cerere (cache HIT)
echo "=== Request 2 (Cache HIT) ==="
time curl -s http://localhost:8080/books > /dev/null

# 4. Verifică logurile
echo "=== Checking logs ==="
docker-compose logs proxy | tail -20
```

## Indicatori de Cache Funcțional

✅ **Cache funcționează dacă:**
- Prima cerere este mai lentă (contactează backend)
- Cererile ulterioare sunt mult mai rapide (< 10ms)
- Logurile arată cache HIT pentru cereri repetate
- Backend-ul nu primește cereri pentru aceeași resursă în intervalul TTL

❌ **Cache nu funcționează dacă:**
- Toate cererile au același timp de răspuns
- Backend-ul primește cereri pentru fiecare request
- Logurile nu arată cache HIT

## Notă Importantă

Ocelot cache-uiește doar **GET requests** care returnează status codes de succes (2xx). 
POST, PUT, DELETE nu sunt cache-uite.

