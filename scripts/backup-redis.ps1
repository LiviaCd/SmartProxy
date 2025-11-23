# Script PowerShell pentru backup Redis (Windows)

$ErrorActionPreference = "Stop"

$BACKUP_DIR = ".\redis-backups"
$TIMESTAMP = Get-Date -Format "yyyyMMdd_HHmmss"
$BACKUP_FILE = "$BACKUP_DIR\redis_backup_$TIMESTAMP.rdb"

Write-Host "Creating Redis backup..."

# Creează directorul dacă nu există
if (-not (Test-Path $BACKUP_DIR)) {
    New-Item -ItemType Directory -Path $BACKUP_DIR | Out-Null
}

# Creează backup folosind BGSAVE
docker exec smartproxy-redis redis-cli BGSAVE

# Așteaptă ca backup-ul să fie complet
Start-Sleep -Seconds 2

# Copiază fișierul RDB
docker cp smartproxy-redis:/data/dump.rdb $BACKUP_FILE

Write-Host "Backup created: $BACKUP_FILE"

# Păstrează doar ultimele 10 backup-uri
Get-ChildItem -Path $BACKUP_DIR -Filter "redis_backup_*.rdb" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -Skip 10 | 
    Remove-Item

Write-Host "Backup completed successfully!"

