# Script PowerShell pentru testare completă SmartProxy

$ErrorActionPreference = "Continue"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "SmartProxy - Test Suite" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Funcție pentru test
function Test-Endpoint {
    param(
        [string]$Url,
        [string]$Description
    )
    
    Write-Host "Testing $Description... " -NoNewline
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "✓ PASS" -ForegroundColor Green
            return $true
        } else {
            Write-Host "✗ FAIL (Status: $($response.StatusCode))" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "✗ FAIL" -ForegroundColor Red
        return $false
    }
}

# 1. Health Checks
Write-Host "=== 1. Health Checks ===" -ForegroundColor Yellow
Test-Endpoint "http://localhost:5000/health" "API 1"
Test-Endpoint "http://localhost:5001/health" "API 2"
Test-Endpoint "http://localhost:5002/health" "API 3"
Test-Endpoint "http://localhost:8080/health" "Proxy"
Write-Host ""

# 2. Create Book
Write-Host "=== 2. Create Book ===" -ForegroundColor Yellow
$bookData = @{
    title = "Test Book"
    author = "Test Author"
    year = 2024
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "http://localhost:8080/books" `
        -Method Post `
        -ContentType "application/json" `
        -Body $bookData
    
    Write-Host "✓ Book created" -ForegroundColor Green
    $bookId = $response.id
    Write-Host "  Book ID: $bookId"
} catch {
    Write-Host "✗ Failed to create book" -ForegroundColor Red
    Write-Host "  Error: $_"
    exit 1
}
Write-Host ""

# 3. Read All Books
Write-Host "=== 3. Read All Books ===" -ForegroundColor Yellow
Test-Endpoint "http://localhost:8080/books" "GET /books"
Write-Host ""

# 4. Cache Test
Write-Host "=== 4. Cache Test ===" -ForegroundColor Yellow
Write-Host "First request (should be slower - cache MISS):"
$stopwatch1 = [System.Diagnostics.Stopwatch]::StartNew()
Invoke-RestMethod -Uri "http://localhost:8080/books" -UseBasicParsing | Out-Null
$stopwatch1.Stop()
Write-Host "  Time: $($stopwatch1.ElapsedMilliseconds)ms"

Write-Host "Second request (should be faster - cache HIT):"
$stopwatch2 = [System.Diagnostics.Stopwatch]::StartNew()
Invoke-RestMethod -Uri "http://localhost:8080/books" -UseBasicParsing | Out-Null
$stopwatch2.Stop()
Write-Host "  Time: $($stopwatch2.ElapsedMilliseconds)ms"

if ($stopwatch2.ElapsedMilliseconds -lt $stopwatch1.ElapsedMilliseconds) {
    Write-Host "✓ Cache working (second request faster)" -ForegroundColor Green
} else {
    Write-Host "⚠ Cache may not be working (times similar)" -ForegroundColor Yellow
}
Write-Host ""

# 5. Read Book by ID
Write-Host "=== 5. Read Book by ID ===" -ForegroundColor Yellow
if ($bookId) {
    Test-Endpoint "http://localhost:8080/books/$bookId" "GET /books/$bookId"
}
Write-Host ""

# 6. Update Book
Write-Host "=== 6. Update Book ===" -ForegroundColor Yellow
if ($bookId) {
    $updateData = @{
        title = "Updated Book"
        author = "Updated Author"
        year = 2025
    } | ConvertTo-Json
    
    try {
        Invoke-RestMethod -Uri "http://localhost:8080/books/$bookId" `
            -Method Put `
            -ContentType "application/json" `
            -Body $updateData | Out-Null
        Write-Host "✓ Book updated" -ForegroundColor Green
    } catch {
        Write-Host "✗ Failed to update book" -ForegroundColor Red
    }
}
Write-Host ""

# 7. Load Balancing Test
Write-Host "=== 7. Load Balancing Test ===" -ForegroundColor Yellow
Write-Host "Making 10 requests..."
for ($i = 1; $i -le 10; $i++) {
    Invoke-RestMethod -Uri "http://localhost:8080/books" -UseBasicParsing | Out-Null
}
Write-Host "✓ 10 requests completed" -ForegroundColor Green
Write-Host "  Check logs to verify distribution: docker-compose logs proxy | Select-String 'Proxying'"
Write-Host ""

# 8. Delete Book
Write-Host "=== 8. Delete Book ===" -ForegroundColor Yellow
if ($bookId) {
    try {
        Invoke-RestMethod -Uri "http://localhost:8080/books/$bookId" `
            -Method Delete | Out-Null
        Write-Host "✓ Book deleted" -ForegroundColor Green
    } catch {
        Write-Host "✗ Failed to delete book" -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Test Suite Completed!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan


