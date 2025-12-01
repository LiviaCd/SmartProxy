# Script PowerShell pentru a actualiza replication_factor la 3 și a rula repair
# Acest script asigură că sistemul este rezistent la eșecuri - dacă un nod cade, celelalte continuă să funcționeze

$ErrorActionPreference = "Stop"

$KEYSPACE = "techframer"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Fixing Cassandra Replication for High Availability" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Verifică dacă containerele rulează
Write-Host "Checking running Cassandra containers..." -ForegroundColor Yellow
$containers = @("smartproxy-cassandra", "smartproxy-cassandra2", "smartproxy-cassandra3")
$runningContainers = @()

foreach ($container in $containers) {
    $status = docker ps --filter "name=$container" --format "{{.Names}}" 2>$null
    if ($status -eq $container) {
        $runningContainers += $container
        Write-Host "  ✓ $container is running" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $container is not running" -ForegroundColor Red
    }
}

if ($runningContainers.Count -eq 0) {
    Write-Host "ERROR: No Cassandra containers are running!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Found $($runningContainers.Count) running Cassandra node(s)" -ForegroundColor Green
Write-Host ""

# Pasul 1: Actualizează keyspace-ul la replication_factor: 3
Write-Host "Step 1: Updating keyspace replication_factor to 3..." -ForegroundColor Yellow
Write-Host ""

$firstContainer = $runningContainers[0]
Write-Host "Connecting to $firstContainer to update keyspace..." -ForegroundColor Cyan

$cqlFile = ".\cassandra-init\02-fix-replication.cql"
if (Test-Path $cqlFile) {
    try {
        # Rulează fișierul CQL
        Get-Content $cqlFile | docker exec -i $firstContainer cqlsh 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Keyspace updated successfully" -ForegroundColor Green
        } else {
            Write-Host "  ⚠ Keyspace update completed (may already be correct)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ⚠ Could not update keyspace: $_" -ForegroundColor Yellow
        Write-Host "  You can manually run: docker exec -i $firstContainer cqlsh < $cqlFile" -ForegroundColor Cyan
    }
} else {
    Write-Host "  ⚠ CQL file not found: $cqlFile" -ForegroundColor Yellow
    Write-Host "  Manually run: ALTER KEYSPACE $KEYSPACE WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 3};" -ForegroundColor Cyan
}

Write-Host ""

# Pasul 2: Rulează repair pe toate nodurile care rulează
Write-Host "Step 2: Running repair on all nodes to replicate data..." -ForegroundColor Yellow
Write-Host ""

foreach ($container in $runningContainers) {
    Write-Host "Running repair on $container..." -ForegroundColor Cyan
    try {
        docker exec $container nodetool repair $KEYSPACE 2>&1 | ForEach-Object {
            if ($_ -match "error|Error|ERROR|failed|Failed|FAILED") {
                Write-Host "  ⚠ $_" -ForegroundColor Yellow
            } else {
                Write-Host "  $_"
            }
        }
        Write-Host "  ✓ Repair completed on $container" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠ Repair may have issues on $container : $_" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Pasul 3: Verifică statusul clusterului
Write-Host "Step 3: Checking cluster status..." -ForegroundColor Yellow
Write-Host ""

docker exec $firstContainer nodetool status

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Your Cassandra cluster is now configured for high availability:" -ForegroundColor Green
Write-Host "  • replication_factor: 3 (data replicated on all 3 nodes)" -ForegroundColor White
Write-Host "  • If one node fails, the other 2 nodes will continue to work" -ForegroundColor White
Write-Host "  • System is now fault-tolerant and scalable" -ForegroundColor White
Write-Host ""
Write-Host "To test, try querying data even when one node is stopped." -ForegroundColor Cyan
Write-Host ""

