#!/bin/bash
# Script pentru restore Cassandra

set -e

if [ -z "$1" ]; then
    echo "Usage: $0 <backup_directory>"
    echo "Example: $0 cassandra-backups/cassandra_backup_20240101_120000"
    exit 1
fi

BACKUP_DIR=$1
KEYSPACE="techframer"

if [ ! -d "$BACKUP_DIR" ]; then
    echo "Error: Backup directory not found: $BACKUP_DIR"
    exit 1
fi

echo "Restoring Cassandra from backup: $BACKUP_DIR"
echo "WARNING: This will overwrite all current data in keyspace: $KEYSPACE"
read -p "Are you sure? (yes/no): " confirm

if [ "$confirm" != "yes" ]; then
    echo "Restore cancelled."
    exit 0
fi

# Oprește Cassandra temporar
echo "Stopping Cassandra..."
docker stop smartproxy-cassandra

# Șterge datele existente
echo "Clearing existing data..."
docker exec smartproxy-cassandra rm -rf /var/lib/cassandra/data/$KEYSPACE/*

# Copiază backup-ul în container
echo "Copying backup data..."
docker cp "$BACKUP_DIR/$KEYSPACE" smartproxy-cassandra:/var/lib/cassandra/data/

# Repornește Cassandra
echo "Starting Cassandra..."
docker start smartproxy-cassandra

# Așteaptă ca Cassandra să fie gata
echo "Waiting for Cassandra to be ready..."
sleep 30

# Reîncarcă snapshot-ul
echo "Reloading snapshot..."
docker exec smartproxy-cassandra nodetool refresh -- "$KEYSPACE" books

echo "Restore completed! Cassandra has been restarted with the backup data."

