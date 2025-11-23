#!/bin/bash
# Script pentru backup Redis

set -e

BACKUP_DIR="./redis-backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/redis_backup_$TIMESTAMP.rdb"

echo "Creating Redis backup..."

# Creează backup folosind BGSAVE
docker exec smartproxy-redis redis-cli BGSAVE

# Așteaptă ca backup-ul să fie complet
while [ "$(docker exec smartproxy-redis redis-cli LASTSAVE)" = "$(docker exec smartproxy-redis redis-cli LASTSAVE)" ]; do
  sleep 1
done

# Copiază fișierul RDB
docker cp smartproxy-redis:/data/dump.rdb "$BACKUP_FILE"

echo "Backup created: $BACKUP_FILE"

# Păstrează doar ultimele 10 backup-uri
ls -t $BACKUP_DIR/redis_backup_*.rdb | tail -n +11 | xargs -r rm

echo "Backup completed successfully!"

