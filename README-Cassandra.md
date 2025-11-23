# Instalare Cassandra în Docker

## Pași pentru a porni Cassandra

1. **Pornește containerul Cassandra:**
   ```bash
   docker-compose up -d
   ```

2. **Verifică statusul:**
   ```bash
   docker-compose ps
   ```

3. **Verifică logurile:**
   ```bash
   docker-compose logs -f cassandra
   ```

## Conectare la Cassandra

### Folosind cqlsh (Command Line Shell)

1. **Intră în container:**
   ```bash
   docker exec -it smartproxy-cassandra cqlsh
   ```

2. **Sau rulează cqlsh direct:**
   ```bash
   docker exec -it smartproxy-cassandra cqlsh localhost
   ```

3. **Comenzi utile în cqlsh:**
   ```cql
   -- Vezi keyspace-urile
   DESCRIBE KEYSPACES;
   
   -- Folosește keyspace-ul
   USE techframer;
   
   -- Vezi tabelele
   DESCRIBE TABLES;
   
   -- Vezi structura tabelului
   DESCRIBE TABLE books;
   
   -- Selectează toate cărțile
   SELECT * FROM books;
   ```

## Configurare în appsettings.json

După ce Cassandra rulează, asigură-te că configurația din `appsettings.json` este corectă:

```json
{
  "Cassandra": {
    "Hosts": "127.0.0.1",
    "Keyspace": "techframer",
    "LocalDatacenter": "datacenter1"
  }
}
```

Dacă rulezi API-ul în Docker, folosește numele serviciului:
```json
{
  "Cassandra": {
    "Hosts": "cassandra",
    "Keyspace": "techframer",
    "LocalDatacenter": "datacenter1"
  }
}
```

## Oprire și ștergere

- **Oprește containerul:**
  ```bash
  docker-compose down
  ```

- **Oprește și șterge volume-ul (datele):**
  ```bash
  docker-compose down -v
  ```

## Testare conexiune

După ce ai pornit Cassandra, poți testa conexiunea prin health check:
```bash
curl http://localhost:5000/health
```

Sau folosește Swagger UI la `http://localhost:5000/swagger` pentru a testa endpoint-urile.

