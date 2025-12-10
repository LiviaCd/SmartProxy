#!/bin/bash
# Script pentru inițializare Cassandra după ce cluster-ul este gata

set -e

echo "⏳ Așteptăm ca Cassandra să fie gata..."

# Așteaptă până când Cassandra este gata
until cqlsh -e "DESCRIBE KEYSPACES" 2>/dev/null; do
    echo "⏳ Cassandra nu este încă gata, așteptăm..."
    sleep 5
done

echo "✅ Cassandra este gata!"

# Rulează scripturile de inițializare
if [ -d "/docker-entrypoint-initdb.d" ]; then
    echo "📝 Rulăm scripturile de inițializare..."
    for f in /docker-entrypoint-initdb.d/*.cql; do
        if [ -f "$f" ]; then
            echo "📄 Executăm: $f"
            cqlsh -f "$f"
        fi
    done
    echo "✅ Inițializare completă!"
fi

