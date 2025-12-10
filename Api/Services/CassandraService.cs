using Cassandra;

namespace Api.Services;

public class CassandraService : IDisposable
{
    private ICluster? _cluster;
    private Cassandra.ISession? _session;
    private readonly ILogger<CassandraService> _logger;
    private bool _initialized = false;
    private readonly IConfiguration _configuration;

    public CassandraService(IConfiguration configuration, ILogger<CassandraService> logger)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private void EnsureConnected()
    {
        if (_initialized) return;
        
        lock (this)
        {
            if (_initialized) return;
            
            try
            {
                // FIXED: Folosește hostname-uri Docker în loc de IP-uri
                var hostsConfig = _configuration["Cassandra:Hosts"] ?? "cassandra,cassandra2,cassandra3";
                var hosts = hostsConfig.Split(",", StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => h.Trim())
                    .ToArray();
                
                var keyspace = _configuration["Cassandra:Keyspace"] ?? "techframer";
                var port = int.Parse(_configuration["Cassandra:Port"] ?? "9042");
                
                // FIXED: Datacenter-ul corect din docker-compose
                var datacenter = _configuration["Cassandra:LocalDatacenter"] ?? "datacenter1";
                
                var username = _configuration["Cassandra:Username"];
                var password = _configuration["Cassandra:Password"];

                _logger.LogInformation("🔄 Connecting to Cassandra: [{Hosts}]:{Port} (DC: {Datacenter})", 
                    string.Join(", ", hosts), port, datacenter);

                var builder = Cluster.Builder()
                    .AddContactPoints(hosts)
                    .WithPort(port)
                    // IMPORTANT: Permite conexiuni la toate nodurile din cluster
                    .WithLoadBalancingPolicy(new DCAwareRoundRobinPolicy(datacenter))
                    .WithReconnectionPolicy(new ConstantReconnectionPolicy(2000))
                    .WithQueryTimeout(30000)
                    .WithSocketOptions(new SocketOptions()
                        .SetConnectTimeoutMillis(10000)
                        .SetReadTimeoutMillis(30000)
                    )
                    // ADDED: Configurări suplimentare pentru stabilitate
                    .WithQueryOptions(new QueryOptions()
                        .SetConsistencyLevel(ConsistencyLevel.One)
                    )
                    .WithPoolingOptions(new PoolingOptions()
                        .SetCoreConnectionsPerHost(HostDistance.Local, 2)
                        .SetMaxConnectionsPerHost(HostDistance.Local, 4)
                    );

                // Autentificare (Cassandra 4.1 din Docker nu necesită credentials by default)
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    _logger.LogInformation("🔐 Using authentication (user: {Username})", username);
                    builder = builder.WithCredentials(username, password);
                }
                else
                {
                    _logger.LogInformation("ℹ️ No credentials provided (using default Cassandra setup)");
                }

                _cluster = builder.Build();

                _logger.LogInformation("🔌 Attempting connection to cluster...");
                
                // Conectează-te la cluster fără keyspace specific
                var tempSession = _cluster.Connect();
                _logger.LogInformation("✅ Connected to Cassandra cluster");

                // Creează keyspace cu replication factor adecvat
                var replicationFactor = hosts.Length >= 3 ? 3 : hosts.Length;
                var createKeyspace = $@"
                    CREATE KEYSPACE IF NOT EXISTS {keyspace}
                    WITH replication = {{'class': 'SimpleStrategy', 'replication_factor': {replicationFactor}}}
                    AND durable_writes = true
                ";
                
                _logger.LogInformation("📦 Creating/verifying keyspace: {Keyspace} (RF: {RF})", 
                    keyspace, replicationFactor);
                tempSession.Execute(createKeyspace);
                _logger.LogInformation("✅ Keyspace ready: {Keyspace}", keyspace);

                // Conectează-te la keyspace specific
                _session = _cluster.Connect(keyspace);
                
                // Creează tabela
                _session.Execute(@"
                    CREATE TABLE IF NOT EXISTS books (
                        id UUID PRIMARY KEY,
                        title TEXT,
                        author TEXT,
                        year INT
                    )
                ");
                _logger.LogInformation("✅ Table 'books' ready");

                tempSession.Dispose();
                _initialized = true;
                
                // Afișează informații despre cluster
                var metadata = _cluster.Metadata;
                var allHosts = metadata.AllHosts();
                _logger.LogInformation("🎉 Cassandra initialized! Connected to {Count} host(s):", allHosts.Count);
                foreach (var host in allHosts)
                {
                    _logger.LogInformation("  📍 {Address} - Datacenter: {DC}, Rack: {Rack}, State: {State}", 
                        host.Address, host.Datacenter, host.Rack, host.IsUp ? "UP" : "DOWN");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to connect to Cassandra: {Message}", ex.Message);
                _initialized = false;
                throw new InvalidOperationException(
                    $"Could not connect to Cassandra. Make sure the cluster is running and accessible. Error: {ex.Message}", 
                    ex);
            }
        }
    }

    public Cassandra.ISession Session 
    { 
        get 
        {
            EnsureConnected();
            return _session!;
        }
    }

    public RowSet ExecuteWithFallback(string query, params object[] values)
    {
        EnsureConnected();
        var statement = new SimpleStatement(query, values);
        statement.SetConsistencyLevel(ConsistencyLevel.One);
        
        try
        {
            return _session!.Execute(statement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Query failed: {Query}", query);
            throw;
        }
    }

    public void Dispose()
    {
        if (_session != null)
        {
            _logger.LogInformation("🔌 Closing Cassandra session...");
            _session.Dispose();
        }
        
        if (_cluster != null)
        {
            _logger.LogInformation("🔌 Closing Cassandra cluster connection...");
            _cluster.Dispose();
        }
    }
}