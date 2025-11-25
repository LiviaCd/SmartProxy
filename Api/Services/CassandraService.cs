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
    /// Executes a parameterized statement with automatic fallback from QUORUM to ONE.
    /// Tries QUORUM first for strong consistency, falls back to ONE if quorum is unavailable.
    /// </summary>
    public RowSet ExecuteWithFallback(string query, params object[] values)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Extract query type for logging
        var queryType = query.TrimStart().Substring(0, Math.Min(6, query.TrimStart().Length)).ToUpper();
        
        try
        {
            // Check if this is a health check query (less verbose logging)
            var isHealthCheck = query.Contains("SELECT now() FROM system.local");
            var logLevel = isHealthCheck ? LogLevel.Debug : LogLevel.Information;
            
            // Log database request
            _logger.Log(
                logLevel,
                "[DATABASE] → {QueryType} | Query:{Query} | Consistency:QUORUM",
                queryType,
                query.Length > 100 ? query.Substring(0, 100) + "..." : query);
            
            // Try with QUORUM first for strong consistency
            var statement = new SimpleStatement(query, values);
            statement.SetConsistencyLevel(ConsistencyLevel.Quorum);
            var result = _session.Execute(statement);
            
            stopwatch.Stop();
            
            // Use same log level as request (Debug for health checks, Information for others)
            _logger.Log(
                logLevel,
                "[DATABASE] ← {QueryType} | ✅ SUCCESS | Consistency:QUORUM | {ElapsedMs}ms",
                queryType,
                stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (UnavailableException ex)
        {
            stopwatch.Stop();
            
            // If quorum is unavailable, fallback to ONE consistency for availability
            _logger.LogWarning(
                "[DATABASE] ⚠️ QUORUM unavailable, falling back to ONE | Query:{Query} | Error:{Error}",
                query.Length > 100 ? query.Substring(0, 100) + "..." : query,
                ex.Message);
            
            var fallbackStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var fallbackStatement = new SimpleStatement(query, values);
            fallbackStatement.SetConsistencyLevel(ConsistencyLevel.One);
            var result = _session.Execute(fallbackStatement);
            
            fallbackStopwatch.Stop();
            
            // Use same log level as request (Debug for health checks, Information for others)
            var isHealthCheck = query.Contains("SELECT now() FROM system.local");
            var logLevel = isHealthCheck ? LogLevel.Debug : LogLevel.Information;
            
            _logger.Log(
                logLevel,
                "[DATABASE] ← {QueryType} | ✅ SUCCESS | Consistency:ONE (fallback) | {ElapsedMs}ms",
                queryType,
                fallbackStopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "[DATABASE] ← {QueryType} | ❌ ERROR | Query:{Query} | {ElapsedMs}ms",
                queryType,
                query.Length > 100 ? query.Substring(0, 100) + "..." : query,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _cluster?.Dispose();
    }
}

