#!/bin/bash
# Script pentru backup Cassandra

set -e

BACKUP_DIR="./cassandra-backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_NAME="cassandra_backup_$TIMESTAMP"
BACKUP_PATH="$BACKUP_DIR/$BACKUP_NAME"
KEYSPACE="techframer"

echo "Creating Cassandra backup for keyspace: $KEYSPACE"

# Creează snapshot
docker exec smartproxy-cassandra nodetool snapshot -t "$BACKUP_NAME" "$KEYSPACE"

# Așteaptă ca snapshot-ul să fie complet
sleep 5

# Copiază snapshot-ul
mkdir -p "$BACKUP_PATH"
docker cp "smartproxy-cassandra:/var/lib/cassandra/data/$KEYSPACE" "$BACKUP_PATH/"

echo "Backup created: $BACKUP_PATH"

# Păstrează doar ultimele 10 backup-uri
ls -td $BACKUP_DIR/cassandra_backup_* 2>/dev/null | tail -n +11 | xargs -r rm -rf

echo "Backup completed successfully!"

