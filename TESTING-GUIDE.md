# Ghid de Testare - SmartProxy

Acest ghid te ajută să rulezi și să testezi toate funcționalitățile sistemului.

## 1. Pornire Servicii

### Pornire completă
```bash
# Pornește toate serviciile
docker-compose up -d

# Verifică statusul
docker-compose ps
```

### Pornire pas cu pas (pentru debugging)
```bash
# 1. Pornește Cassandra (așteaptă să fie gata)
docker-compose up -d cassandra cassandra2
docker-compose logs -f cassandra  # Așteaptă "Starting listening for CQL clients"

# 2. Pornește Redis
docker-compose up -d redis
docker-compose logs -f redis  # Așteaptă "Ready to accept connections"

# 3. Pornește API-urile
docker-compose up -d api1 api2 api3
docker-compose logs -f api1  # Verifică că s-a conectat la Cassandra

# 4. Pornește Proxy
docker-compose up -d proxy
docker-compose logs -f proxy  # Verifică că Ocelot a pornit
```

## 2. Verificare Health Checks

### Verificare toate serviciile
```bash
# Cassandra
docker exec -it smartproxy-cassandra nodetool status

# Redis
docker exec -it smartproxy-redis redis-cli ping

# API-uri
curl http://localhost:5000/health
curl http://localhost:5001/health
curl http://localhost:5002/health

# Proxy (forward-ează către backend)
curl http://localhost:8080/health
```

## 3. Testare API Direct (fără proxy)

### Test CRUD Operations
```bash
# CREATE - Creează o carte
curl -X POST http://localhost:5000/books \
  -H "Content-Type: application/json" \
  -d '{"title":"Test Book","author":"Test Author","year":2024}'

# Salvează ID-ul returnat pentru testele următoare
BOOK_ID="<id-ul-returnat>"

# READ ALL - Listă toate cărțile
curl http://localhost:5000/books

# READ BY ID - Obține o carte specifică
curl http://localhost:5000/books/$BOOK_ID

# UPDATE - Actualizează o carte
curl -X PUT http://localhost:5000/books/$BOOK_ID \
  -H "Content-Type: application/json" \
  -d '{"title":"Updated Book","author":"Updated Author","year":2025}'

# DELETE - Șterge o carte
curl -X DELETE http://localhost:5000/books/$BOOK_ID
```

### Test Format Negotiation
```bash
# JSON (default)
curl -H "Accept: application/json" http://localhost:5000/books

# XML
curl -H "Accept: application/xml" http://localhost:5000/books
```

## 4. Testare Load Balancing

### Test Round-Robin Distribution
```bash
# Face 10 cereri și observă distribuția
for i in {1..10}; do
  echo "Request $i:"
  curl -s http://localhost:8080/books | head -1
  echo ""
done
```

**Rezultat așteptat:**
- Cererile ar trebui să fie distribuite între api1, api2, api3
- Poți verifica în loguri care backend a procesat fiecare cerere

### Verificare în Loguri
```bash
# Vezi care backend procesează fiecare cerere
docker-compose logs proxy | grep "Proxying"
```

## 5. Testare Caching

### Test Cache Hit/Miss
```bash
# Prima cerere - cache MISS (va contacta backend)
echo "=== Request 1 (Cache MISS) ==="
time curl -v http://localhost:8080/books 2>&1 | grep -E "(time|HTTP)"

# A doua cerere - cache HIT (din cache, mult mai rapidă)
echo "=== Request 2 (Cache HIT) ==="
time curl -v http://localhost:8080/books 2>&1 | grep -E "(time|HTTP)"
```

### Test Cache Expiration
```bash
# Face o cerere
curl http://localhost:8080/books

# Așteaptă 5 minute (sau modifică TTL în ocelot.json la 10 secunde pentru test rapid)
# Apoi face o altă cerere - ar trebui să fie cache MISS
curl http://localhost:8080/books
```

### Verificare Cache în Loguri
```bash
# Vezi logurile cache
docker-compose logs proxy | grep -i "cache\|Cache"
```

## 6. Testare End-to-End

### Script complet de testare
```bash
#!/bin/bash

echo "=== 1. Health Checks ==="
curl http://localhost:8080/health
echo ""

echo "=== 2. Create Book ==="
RESPONSE=$(curl -s -X POST http://localhost:8080/books \
  -H "Content-Type: application/json" \
  -d '{"title":"E2E Test","author":"Test Author","year":2024}')
echo $RESPONSE
BOOK_ID=$(echo $RESPONSE | grep -o '"id":"[^"]*' | cut -d'"' -f4)
echo "Book ID: $BOOK_ID"
echo ""

echo "=== 3. Read All Books (Cache Test) ==="
echo "First request (MISS):"
time curl -s http://localhost:8080/books > /dev/null
echo "Second request (HIT):"
time curl -s http://localhost:8080/books > /dev/null
echo ""

echo "=== 4. Read Book by ID ==="
curl http://localhost:8080/books/$BOOK_ID
echo ""

echo "=== 5. Update Book ==="
curl -X PUT http://localhost:8080/books/$BOOK_ID \
  -H "Content-Type: application/json" \
  -d '{"title":"Updated E2E Test","author":"Updated Author","year":2025}'
echo ""

echo "=== 6. Delete Book ==="
curl -X DELETE http://localhost:8080/books/$BOOK_ID
echo ""

echo "=== 7. Verify Deleted ==="
curl http://localhost:8080/books/$BOOK_ID
echo ""
```

## 7. Testare Cassandra Cluster

### Verificare Cluster Status
```bash
# Verifică statusul cluster-ului
docker exec -it smartproxy-cassandra nodetool status

# Ar trebui să vezi ambele noduri ca UN (Up Normal)
```

### Test Replicare Date
```bash
# Adaugă date prin API
curl -X POST http://localhost:5000/books \
  -H "Content-Type: application/json" \
  -d '{"title":"Replication Test","author":"Author","year":2024}'

# Verifică pe nodul 1
docker exec -it smartproxy-cassandra cqlsh -e "SELECT * FROM techframer.books;"

# Verifică pe nodul 2 (ar trebui să vezi aceleași date)
docker exec -it smartproxy-cassandra2 cqlsh -e "SELECT * FROM techframer.books;"
```

## 8. Monitorizare în Timp Real

### Loguri Live
```bash
# Toate serviciile
docker-compose logs -f

# Doar proxy
docker-compose logs -f proxy

# Doar API-uri
docker-compose logs -f api1 api2 api3

# Doar Cassandra
docker-compose logs -f cassandra cassandra2
```

### Statistici Container
```bash
# Vezi utilizarea resurselor
docker stats

# Vezi procesele dintr-un container
docker exec -it smartproxy-proxy ps aux
```

## 9. Testare Performanță

### Test Load (Multiple Requests)
```bash
# Face 100 de cereri simultane
for i in {1..100}; do
  curl -s http://localhost:8080/books > /dev/null &
done
wait
echo "100 requests completed"
```

### Test cu Apache Bench (dacă e instalat)
```bash
# 100 cereri, 10 simultane
ab -n 100 -c 10 http://localhost:8080/books
```

## 10. Debugging

### Verificare Configurație
```bash
# Verifică configurația Ocelot
docker exec -it smartproxy-proxy cat /app/ocelot.json

# Verifică variabilele de mediu
docker exec -it smartproxy-proxy env | grep -i cassandra
```

### Verificare Conectivitate
```bash
# Test conectivitate între containere
docker exec -it smartproxy-proxy ping -c 3 api1
docker exec -it smartproxy-proxy ping -c 3 api2
docker exec -it smartproxy-proxy ping -c 3 api3
docker exec -it smartproxy-proxy ping -c 3 cassandra
```

### Verificare Porturi
```bash
# Vezi ce porturi sunt deschise
docker-compose ps
netstat -an | grep -E "5000|5001|5002|8080|9042|6379"
```

## 11. Curățare și Restart

### Restart Servicii
```bash
# Restart un serviciu specific
docker-compose restart proxy

# Restart toate serviciile
docker-compose restart
```

### Curățare Cache
```bash
# Restart proxy pentru a curăța cache-ul Ocelot
docker-compose restart proxy
```

### Curățare Date
```bash
# Șterge toate datele (ATENȚIE: șterge tot!)
docker-compose down -v

# Șterge doar containerele (păstrează datele)
docker-compose down
```

## 12. Checklist Testare

- [ ] Toate containerele pornite și healthy
- [ ] Cassandra cluster cu 2 noduri UN
- [ ] Redis conectat și funcțional
- [ ] API-urile răspund la /health
- [ ] Proxy forward-ează cererile corect
- [ ] Load balancing distribuie între 3 API-uri
- [ ] Cache funcționează (a doua cerere mai rapidă)
- [ ] CRUD operations funcționează prin proxy
- [ ] Format negotiation (JSON/XML) funcționează
- [ ] Datele sunt replicate în ambele noduri Cassandra

## Probleme Comune

### Container nu pornește
```bash
# Verifică logurile pentru erori
docker-compose logs <service-name>

# Verifică dacă portul e deja folosit
netstat -an | grep <port>
```

### Cache nu funcționează
- Verifică că `FileCacheOptions` este configurat în `ocelot.json`
- Verifică că cererea este GET (doar GET requests sunt cache-uite)
- Verifică logurile pentru erori

### Load balancing nu funcționează
- Verifică că toate API-urile sunt în `DownstreamHostAndPorts`
- Verifică că `LoadBalancerOptions.Type` este "RoundRobin"
- Verifică logurile pentru a vedea care backend primește cererile

