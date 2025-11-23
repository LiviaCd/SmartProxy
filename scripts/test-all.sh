#!/bin/bash
# Script complet de testare pentru SmartProxy

set -e

echo "========================================="
echo "SmartProxy - Test Suite"
echo "========================================="
echo ""

# Culori pentru output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Funcție pentru test
test_endpoint() {
    local url=$1
    local description=$2
    
    echo -n "Testing $description... "
    if curl -s -f -o /dev/null "$url"; then
        echo -e "${GREEN}✓ PASS${NC}"
        return 0
    else
        echo -e "${RED}✗ FAIL${NC}"
        return 1
    fi
}

# 1. Health Checks
echo "=== 1. Health Checks ==="
test_endpoint "http://localhost:5000/health" "API 1"
test_endpoint "http://localhost:5001/health" "API 2"
test_endpoint "http://localhost:5002/health" "API 3"
test_endpoint "http://localhost:8080/health" "Proxy"
echo ""

# 2. Cassandra Cluster
echo "=== 2. Cassandra Cluster ==="
echo -n "Checking Cassandra cluster status... "
if docker exec smartproxy-cassandra nodetool status | grep -q "UN.*rack1"; then
    echo -e "${GREEN}✓ Node 1 UP${NC}"
else
    echo -e "${RED}✗ Node 1 DOWN${NC}"
fi

if docker exec smartproxy-cassandra nodetool status | grep -q "UN.*rack2"; then
    echo -e "${GREEN}✓ Node 2 UP${NC}"
else
    echo -e "${RED}✗ Node 2 DOWN${NC}"
fi
echo ""

# 3. Redis
echo "=== 3. Redis ==="
echo -n "Checking Redis... "
if docker exec smartproxy-redis redis-cli ping | grep -q "PONG"; then
    echo -e "${GREEN}✓ Redis UP${NC}"
else
    echo -e "${RED}✗ Redis DOWN${NC}"
fi
echo ""

# 4. Create Book
echo "=== 4. Create Book ==="
RESPONSE=$(curl -s -X POST http://localhost:8080/books \
  -H "Content-Type: application/json" \
  -d '{"title":"Test Book","author":"Test Author","year":2024}')

if echo "$RESPONSE" | grep -q "id"; then
    echo -e "${GREEN}✓ Book created${NC}"
    BOOK_ID=$(echo $RESPONSE | grep -o '"id":"[^"]*' | cut -d'"' -f4)
    echo "  Book ID: $BOOK_ID"
else
    echo -e "${RED}✗ Failed to create book${NC}"
    echo "  Response: $RESPONSE"
    exit 1
fi
echo ""

# 5. Read All Books
echo "=== 5. Read All Books ==="
test_endpoint "http://localhost:8080/books" "GET /books"
echo ""

# 6. Cache Test
echo "=== 6. Cache Test ==="
echo "First request (should be slower - cache MISS):"
TIME1=$(curl -o /dev/null -s -w '%{time_total}' http://localhost:8080/books)
echo "  Time: ${TIME1}s"

echo "Second request (should be faster - cache HIT):"
TIME2=$(curl -o /dev/null -s -w '%{time_total}' http://localhost:8080/books)
echo "  Time: ${TIME2}s"

if (( $(echo "$TIME2 < $TIME1" | bc -l) )); then
    echo -e "${GREEN}✓ Cache working (second request faster)${NC}"
else
    echo -e "${YELLOW}⚠ Cache may not be working (times similar)${NC}"
fi
echo ""

# 7. Read Book by ID
echo "=== 7. Read Book by ID ==="
if [ ! -z "$BOOK_ID" ]; then
    test_endpoint "http://localhost:8080/books/$BOOK_ID" "GET /books/$BOOK_ID"
fi
echo ""

# 8. Update Book
echo "=== 8. Update Book ==="
if [ ! -z "$BOOK_ID" ]; then
    if curl -s -X PUT "http://localhost:8080/books/$BOOK_ID" \
      -H "Content-Type: application/json" \
      -d '{"title":"Updated Book","author":"Updated Author","year":2025}' | grep -q "updated"; then
        echo -e "${GREEN}✓ Book updated${NC}"
    else
        echo -e "${RED}✗ Failed to update book${NC}"
    fi
fi
echo ""

# 9. Load Balancing Test
echo "=== 9. Load Balancing Test ==="
echo "Making 10 requests to check distribution..."
for i in {1..10}; do
    curl -s http://localhost:8080/books > /dev/null
done
echo -e "${GREEN}✓ 10 requests completed${NC}"
echo "  Check logs to verify distribution: docker-compose logs proxy | grep Proxying"
echo ""

# 10. Delete Book
echo "=== 10. Delete Book ==="
if [ ! -z "$BOOK_ID" ]; then
    if curl -s -X DELETE "http://localhost:8080/books/$BOOK_ID" | grep -q "deleted"; then
        echo -e "${GREEN}✓ Book deleted${NC}"
    else
        echo -e "${RED}✗ Failed to delete book${NC}"
    fi
fi
echo ""

echo "========================================="
echo -e "${GREEN}Test Suite Completed!${NC}"
echo "========================================="

