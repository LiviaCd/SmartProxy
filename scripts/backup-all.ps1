# Script PowerShell pentru backup complet (Windows)

Write-Host "========================================="
Write-Host "Starting full backup of all data..."
Write-Host "========================================="

# Backup Redis
Write-Host ""
Write-Host "1. Backing up Redis..."
& ".\scripts\backup-redis.ps1"

# Backup Cassandra
Write-Host ""
Write-Host "2. Backing up Cassandra..."
& ".\scripts\backup-cassandra.ps1"

Write-Host ""
Write-Host "========================================="
Write-Host "Full backup completed successfully!"
Write-Host "========================================="

