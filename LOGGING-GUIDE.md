# Ghid pentru Vizualizarea Logurilor

Acest ghid explică cum să vizualizezi logurile pentru a monitoriza fluxul de date în SmartProxy.

## 1. Vizualizare Loguri în Docker (Recomandat)

### Toate logurile (toate serviciile)
```bash
docker-compose logs
```

### Doar logurile proxy-ului
```bash
docker-compose logs proxy
```

### Loguri în timp real (follow mode)
```bash
# Toate serviciile
docker-compose logs -f

# Doar proxy-ul
docker-compose logs -f proxy

# Proxy + API-uri
docker-compose logs -f proxy api1 api2 api3
```

### Ultimele N linii
```bash
# Ultimele 100 de linii
docker-compose logs --tail=100 proxy

# Ultimele 50 de linii în timp real
docker-compose logs -f --tail=50 proxy
```

## 2. Filtrare Loguri

### Filtrare după tip de log
```bash
# Doar logurile de cereri primite
docker-compose logs proxy | grep "INCOMING REQUEST"

# Doar logurile de răspunsuri
docker-compose logs proxy | grep "OUTGOING RESPONSE"

# Doar logurile de downstream
docker-compose logs proxy | grep "DOWNSTREAM"

# Doar logurile de cache
docker-compose logs proxy | grep -i "cache"

# Doar logurile JSON
docker-compose logs proxy | grep "Format: JSON"

# Doar logurile XML
docker-compose logs proxy | grep "Format: XML"
```

### Filtrare după endpoint
```bash
# Toate cererile către /books
docker-compose logs proxy | grep "/books"

# Cereri cu Accept header
docker-compose logs proxy | grep "Accept:"
```

### Filtrare combinată
```bash
# Cereri JSON către /books
docker-compose logs proxy | grep "/books" | grep "JSON"

# Cache hits pentru /books
docker-compose logs proxy | grep "/books" | grep -i "HIT"
```

## 3. Vizualizare Structurată

### Loguri cu timestamp
```bash
# Loguri cu timestamp
docker-compose logs -t proxy

# Loguri cu timestamp în timp real
docker-compose logs -f -t proxy
```

### Export loguri în fișier
```bash
# Export toate logurile
docker-compose logs proxy > proxy-logs.txt

# Export doar logurile de astăzi
docker-compose logs --since 24h proxy > proxy-logs-today.txt

# Export loguri cu timestamp
docker-compose logs -t proxy > proxy-logs-timestamped.txt
```

## 4. Monitorizare în Timp Real

### Terminal 1: Loguri proxy
```bash
docker-compose logs -f proxy
```

### Terminal 2: Testează API-ul
```bash
# Test JSON
curl http://localhost:8080/books -H "Accept: application/json"

# Test XML
curl http://localhost:8080/books -H "Accept: application/xml"
```

În Terminal 1 vei vedea imediat logurile generate.

## 5. Exemple de Loguri

### Cerere primită (INCOMING REQUEST)
```
[INCOMING REQUEST] GET /books | Client IP: 172.18.0.1 | Accept: application/json | Content-Type: none | User-Agent: curl/7.68.0
```

### Cerere către downstream (DOWNSTREAM REQUEST)
```
[DOWNSTREAM REQUEST] GET http://api1:8080/books | Accept: application/json | Content-Type: none
```

### Răspuns downstream (DOWNSTREAM RESPONSE)
```
[DOWNSTREAM RESPONSE] GET /books | Status: 200 | Format: JSON | Cache: MISS | Time: 45ms | Size: 1024 bytes
```

### Răspuns trimis clientului (OUTGOING RESPONSE)
```
[OUTGOING RESPONSE] GET /books | Status: 200 | Format: JSON | Time: 48ms | Size: 1024 bytes | Accept Requested: application/json
```

## 6. Scripturi Utile

### Script pentru monitorizare continuă
Creează un fișier `monitor-logs.sh`:
```bash
#!/bin/bash
echo "Monitoring SmartProxy logs..."
docker-compose logs -f proxy | grep -E "INCOMING|OUTGOING|DOWNSTREAM|Cache"
```

### Script pentru analiză cache
Creează un fișier `analyze-cache.sh`:
```bash
#!/bin/bash
echo "Cache Statistics:"
echo "=================="
echo "Cache HITs:"
docker-compose logs proxy | grep -i "HIT" | wc -l
echo "Cache MISSes:"
docker-compose logs proxy | grep -i "MISS" | wc -l
echo ""
echo "Format Distribution:"
echo "JSON responses:"
docker-compose logs proxy | grep "Format: JSON" | wc -l
echo "XML responses:"
docker-compose logs proxy | grep "Format: XML" | wc -l
```

## 7. Vizualizare în Browser (Opțional)

Pentru o vizualizare mai avansată, poți folosi:
- **Portainer** - UI pentru Docker
- **Grafana Loki** - Aggregation și vizualizare loguri
- **ELK Stack** - Elasticsearch, Logstash, Kibana

## 8. Comenzi Rapide

```bash
# Loguri proxy în timp real
docker-compose logs -f proxy

# Ultimele 50 de linii
docker-compose logs --tail=50 proxy

# Loguri doar pentru /books
docker-compose logs proxy | grep "/books"

# Loguri cu cache status
docker-compose logs proxy | grep -i "cache"

# Export loguri
docker-compose logs proxy > logs.txt
```

## 9. Debugging

### Loguri detaliate pentru debugging
Modifică `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Proxy.Middleware.RequestLoggingMiddleware": "Debug",
      "Proxy.DelegatingHandlers.CacheLoggingHandler": "Debug"
    }
  }
}
```

Apoi reconstruiește containerul:
```bash
docker-compose up -d --build proxy
```

## 10. Curățare Loguri

### Ștergere loguri vechi
```bash
# Șterge logurile containerului (nu datele!)
docker-compose logs --no-log-prefix proxy > /dev/null
```

### Resetare completă
```bash
# Oprește și șterge containerele (atenție: șterge și datele dacă nu sunt în volume!)
docker-compose down
```

