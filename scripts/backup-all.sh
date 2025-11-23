#!/bin/bash
# Script pentru backup complet al tuturor datelor

set -e

echo "========================================="
echo "Starting full backup of all data..."
echo "========================================="

# Backup Redis
echo ""
echo "1. Backing up Redis..."
./scripts/backup-redis.sh

# Backup Cassandra
echo ""
echo "2. Backing up Cassandra..."
./scripts/backup-cassandra.sh

echo ""
echo "========================================="
echo "Full backup completed successfully!"
echo "========================================="

