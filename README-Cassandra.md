# Instalare Cassandra în Docker

## Configurare Cluster cu 2 Noduri

Proiectul folosește un cluster Cassandra cu **2 noduri** pentru:
- **Replicare**: Datele sunt replicate pe ambele noduri (replication_factor: 2)
- **Disponibilitate ridicată**: Dacă un nod eșuează, celălalt poate continua să servească date
- **Scalabilitate**: Poți adăuga mai multe noduri ulterior

## Pași pentru a porni Cassandra

1. **Pornește cluster-ul Cassandra:**
   ```bash
   docker-compose up -d cassandra cassandra2
   ```

2. **Verifică statusul:**
   ```bash
   docker-compose ps
   ```

3. **Verifică logurile:**
   ```bash
   docker-compose logs -f cassandra
   docker-compose logs -f cassandra2
   ```

4. **Verifică statusul cluster-ului:**
   ```bash
   docker exec -it smartproxy-cassandra nodetool status
   ```
   
   Ar trebui să vezi ambele noduri ca `UN` (Up Normal):
   ```
   Datacenter: datacenter1
   =======================
   Status=Up/Down
   |/ State=Normal/Leaving/Joining/Moving
   --  Address     Load       Tokens       Owns    Host ID                               Rack
   UN  172.18.0.x  123.45 KB  256          ?       xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx  rack1
   UN  172.18.0.y  123.45 KB  256          ?       yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy  rack2
   ```

## Conectare la Cassandra

### Folosind cqlsh (Command Line Shell)

1. **Intră în container (nodul 1):**
   ```bash
   docker exec -it smartproxy-cassandra cqlsh
   ```

2. **Sau conectează-te la nodul 2:**
   ```bash
   docker exec -it smartproxy-cassandra2 cqlsh
   ```

3. **Sau rulează cqlsh direct:**
   ```bash
   docker exec -it smartproxy-cassandra cqlsh localhost
   docker exec -it smartproxy-cassandra2 cqlsh localhost
   ```

### Porturi

- **Cassandra Node 1**: 
  - CQL: `localhost:9042`
  - Inter-node: `localhost:7000`
  
- **Cassandra Node 2**: 
  - CQL: `localhost:9043` (port diferit pentru acces local)
  - Inter-node: `localhost:7002`

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

