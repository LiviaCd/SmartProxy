-- Create keyspace with replication factor 3 for 3-node cluster
CREATE KEYSPACE IF NOT EXISTS techframer
WITH replication = {
    'class': 'SimpleStrategy',
    'replication_factor': 3
};

USE techframer;

-- Create books table
CREATE TABLE IF NOT EXISTS books (
    id UUID PRIMARY KEY,
    title TEXT,
    author TEXT,
    year INT
);

