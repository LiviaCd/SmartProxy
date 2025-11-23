#!/bin/bash
# Script pentru restore Redis

set -e

if [ -z "$1" ]; then
    echo "Usage: $0 <backup_file.rdb>"
    echo "Example: $0 redis-backups/redis_backup_20240101_120000.rdb"
    exit 1
fi

BACKUP_FILE=$1

if [ ! -f "$BACKUP_FILE" ]; then
    echo "Error: Backup file not found: $BACKUP_FILE"
    exit 1
fi

echo "Restoring Redis from backup: $BACKUP_FILE"
echo "WARNING: This will overwrite all current data in Redis!"
read -p "Are you sure? (yes/no): " confirm

if [ "$confirm" != "yes" ]; then
    echo "Restore cancelled."
    exit 0
fi

# Oprește Redis temporar (opțional, poate rula și cu Redis activ)
# docker stop smartproxy-redis

# Copiază backup-ul în container
docker cp "$BACKUP_FILE" smartproxy-redis:/data/dump.rdb

# Repornește Redis pentru a încărca backup-ul
docker restart smartproxy-redis

echo "Restore completed! Redis has been restarted with the backup data."

