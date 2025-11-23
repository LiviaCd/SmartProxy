-- Create keyspace with replication factor 2 for 2-node cluster
CREATE KEYSPACE IF NOT EXISTS techframer
WITH replication = {
    'class': 'SimpleStrategy',
    'replication_factor': 2
};

USE techframer;

-- Create books table
CREATE TABLE IF NOT EXISTS books (
    id UUID PRIMARY KEY,
    title TEXT,
    author TEXT,
    year INT
);

