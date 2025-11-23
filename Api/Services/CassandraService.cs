using Cassandra;

namespace Api.Services;

public class CassandraService : IDisposable
{
    private readonly ICluster _cluster;
    private readonly Cassandra.ISession _session;
    private readonly ILogger<CassandraService> _logger;

    public CassandraService(IConfiguration configuration, ILogger<CassandraService> logger)
    {
        _logger = logger;
        
        var hosts = configuration["Cassandra:Hosts"]?.Split(",") ?? new[] { "127.0.0.1" };
        var keyspace = configuration["Cassandra:Keyspace"] ?? "techframer";
        var localDc = configuration["Cassandra:LocalDatacenter"] ?? "datacenter1";

        // Configure load balancing policy
        // TokenAwarePolicy - routes requests to the node that owns the data (token-aware)
        // DCAwareRoundRobinPolicy - distributes requests round-robin in datacenter
        var loadBalancingPolicy = new TokenAwarePolicy(
            new DCAwareRoundRobinPolicy(localDc)
        );

        // Configure retry policy
        var retryPolicy = new DefaultRetryPolicy();

        // Create cluster with load balancing
        // Protocol version is negotiated automatically by the driver
        _cluster = Cluster.Builder()
            .AddContactPoints(hosts)
            .WithLoadBalancingPolicy(loadBalancingPolicy)
            .WithRetryPolicy(retryPolicy)
            .Build();

        // Connect without keyspace first to check/create it
        var sessionWithoutKeyspace = _cluster.Connect();
        
        // Create keyspace if it doesn't exist
        try
        {
            sessionWithoutKeyspace.Execute(new SimpleStatement($@"
                CREATE KEYSPACE IF NOT EXISTS {keyspace}
                WITH replication = {{
                    'class': 'SimpleStrategy',
                    'replication_factor': 1
                }}"));
            
            _logger.LogInformation("Keyspace '{Keyspace}' created or already exists", keyspace);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error creating keyspace '{Keyspace}', it may already exist", keyspace);
        }

        // Now connect to the keyspace
        _session = _cluster.Connect(keyspace);
        
        // Create books table if it doesn't exist
        try
        {
            _session.Execute(new SimpleStatement(@"
                CREATE TABLE IF NOT EXISTS books (
                    id UUID PRIMARY KEY,
                    title TEXT,
                    author TEXT,
                    year INT
                )"));
            
            _logger.LogInformation("Table 'books' created or already exists");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error creating table 'books', it may already exist");
        }
        
        sessionWithoutKeyspace.Dispose();
    }

    public Cassandra.ISession Session => _session;

    /// <summary>
    /// Creates a SimpleStatement with QUORUM consistency level
    /// </summary>
    public SimpleStatement CreateStatement(string query, params object[] values)
    {
        var statement = new SimpleStatement(query, values);
        statement.SetConsistencyLevel(ConsistencyLevel.Quorum);
        return statement;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _cluster?.Dispose();
    }
}

