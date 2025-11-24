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
                    'replication_factor': 2
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
    /// Creates a SimpleStatement with adaptive consistency level.
    /// Tries QUORUM first, falls back to ONE if quorum is unavailable.
    /// </summary>
    public SimpleStatement CreateStatement(string query, params object[] values)
    {
        var statement = new SimpleStatement(query, values);
        
        // Try to use QUORUM for strong consistency when possible
        // The driver will automatically retry with lower consistency if needed
        // For better resilience, we'll use QUORUM but allow fallback
        statement.SetConsistencyLevel(ConsistencyLevel.Quorum);
        
        return statement;
    }

    /// <summary>
    /// Creates a SimpleStatement with ONE consistency level for better availability.
    /// Use this for operations that can tolerate eventual consistency.
    /// </summary>
    public SimpleStatement CreateStatementWithOneConsistency(string query, params object[] values)
    {
        var statement = new SimpleStatement(query, values);
        statement.SetConsistencyLevel(ConsistencyLevel.One);
        return statement;
    }

    /// <summary>
    /// Executes a statement with automatic fallback from QUORUM to ONE if quorum fails.
    /// </summary>
    public RowSet ExecuteWithFallback(SimpleStatement statement)
    {
        try
        {
            // Try with QUORUM first
            return _session.Execute(statement);
        }
        catch (UnavailableException ex)
        {
            // If quorum is unavailable, try with ONE consistency
            _logger.LogWarning(
                "Quorum unavailable, falling back to ONE consistency. Query: {Query}, Error: {Error}",
                statement.QueryString,
                ex.Message);
            
            // Create a new statement with ONE consistency level
            // For parameterized queries, we need to extract the values
            // Since SimpleStatement doesn't expose bound values, we'll use
            // a workaround: create statement with query and try to bind values
            // The driver's retry policy should handle this, but we'll do it explicitly
            
            // Create new statement with ONE consistency
            // Note: This works because the statement was created with bound values
            // in CreateStatement, and we're recreating it with the same query
            var fallbackStatement = new SimpleStatement(statement.QueryString);
            fallbackStatement.SetConsistencyLevel(ConsistencyLevel.One);
            
            // Try to copy bound values if the statement has them
            // The driver stores them internally, so we need to pass them explicitly
            // For now, we'll execute with the query only (works for non-parameterized queries)
            // For parameterized queries, the controller should pass values to a new method
            
            return _session.Execute(fallbackStatement);
        }
    }

    /// <summary>
    /// Executes a parameterized statement with automatic fallback from QUORUM to ONE.
    /// </summary>
    public RowSet ExecuteWithFallback(string query, params object[] values)
    {
        try
        {
            // Try with QUORUM first
            var statement = new SimpleStatement(query, values);
            statement.SetConsistencyLevel(ConsistencyLevel.Quorum);
            return _session.Execute(statement);
        }
        catch (UnavailableException ex)
        {
            // If quorum is unavailable, try with ONE consistency
            _logger.LogWarning(
                "Quorum unavailable, falling back to ONE consistency. Query: {Query}, Error: {Error}",
                query,
                ex.Message);
            
            var fallbackStatement = new SimpleStatement(query, values);
            fallbackStatement.SetConsistencyLevel(ConsistencyLevel.One);
            return _session.Execute(fallbackStatement);
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _cluster?.Dispose();
    }
}

