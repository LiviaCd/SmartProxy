# Script PowerShell pentru backup Cassandra (Windows)

$ErrorActionPreference = "Stop"

$BACKUP_DIR = ".\cassandra-backups"
$TIMESTAMP = Get-Date -Format "yyyyMMdd_HHmmss"
$BACKUP_NAME = "cassandra_backup_$TIMESTAMP"
$BACKUP_PATH = "$BACKUP_DIR\$BACKUP_NAME"
$KEYSPACE = "techframer"

Write-Host "Creating Cassandra backup for keyspace: $KEYSPACE"

# Creează directorul dacă nu există
if (-not (Test-Path $BACKUP_DIR)) {
    New-Item -ItemType Directory -Path $BACKUP_DIR | Out-Null
}

# Creează snapshot
docker exec smartproxy-cassandra nodetool snapshot -t $BACKUP_NAME $KEYSPACE

# Așteaptă ca snapshot-ul să fie complet
Start-Sleep -Seconds 5

# Copiază snapshot-ul
New-Item -ItemType Directory -Path $BACKUP_PATH -Force | Out-Null
docker cp "smartproxy-cassandra:/var/lib/cassandra/data/$KEYSPACE" "$BACKUP_PATH\"

Write-Host "Backup created: $BACKUP_PATH"

# Păstrează doar ultimele 10 backup-uri
Get-ChildItem -Path $BACKUP_DIR -Directory -Filter "cassandra_backup_*" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -Skip 10 | 
    Remove-Item -Recurse -Force

Write-Host "Backup completed successfully!"

