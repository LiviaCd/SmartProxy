-- Fix replication factor to 3 for high availability
-- This ensures data is replicated on all 3 nodes
-- If one node fails, the other 2 nodes will continue to work

ALTER KEYSPACE techframer 
WITH replication = {
    'class': 'SimpleStrategy',
    'replication_factor': 3
};

