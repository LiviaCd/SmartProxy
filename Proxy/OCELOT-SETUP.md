# Configurare Ocelot pentru Smart Proxy

Proxy-ul folosește **doar Ocelot** pentru toate funcționalitățile de API Gateway.

## Pachete NuGet

- `Ocelot` - API Gateway principal

## Configurare ocelot.json

Fișierul `ocelot.json` definește:
- **Routes**: Rutele care vor fi proxy-uite
- **LoadBalancerOptions**: Algoritmul de load balancing (RoundRobin)
- **FileCacheOptions**: Configurarea cache-ului (TTL 300 secunde)
- **QoSOptions**: Quality of Service (circuit breaker, timeout)
- **GlobalConfiguration**: Configurare globală (BaseUrl)

**Notă**: Conform [documentației oficiale Ocelot](https://ocelot.readthedocs.io/en/latest/introduction/gettingstarted.html#net-core-3-1), folosim `AddOcelot()` pentru single file mode.

## Funcționalități

### 1. Load Balancing (Round-Robin)

Ocelot distribuie automat cererile între `api1:8080`, `api2:8080` și `api3:8080` folosind algoritmul Round-Robin.

### 2. Caching

Ocelot cache-uiește automat răspunsurile pentru rutele configurate cu `FileCacheOptions`. Cache-ul este în-memory și expiră după 300 secunde.

### 3. Routing

- `/{everything}` - Proxy-ează toate cererile către backend-uri
- `/health` - Proxy-ează health check-urile
- `/api/{everything}` - Rutele API proprii (ex: `/api/cache/keys`)

### 4. Quality of Service

- **Circuit Breaker**: Dacă un backend eșuează de 3 ori, se oprește temporar (1 secundă)
- **Timeout**: 5 secunde pentru fiecare cerere

## Diferențe față de implementarea custom

### Avantaje Ocelot:
- ✅ Load balancing built-in (mai multe opțiuni: RoundRobin, LeastConnection, etc.)
- ✅ Caching built-in
- ✅ Circuit breaker și retry logic
- ✅ Configurare declarativă (JSON)
- ✅ Suport pentru service discovery
- ✅ Rate limiting
- ✅ Authentication/Authorization

### Limitări față de implementarea custom:
- ⚠️ Cache-ul este în-memory (nu Redis) - poate fi configurat cu Redis separat
- ⚠️ Invalidarea cache-ului la modificări trebuie gestionată manual

## Configurare Redis Cache (opțional)

Pentru a folosi Redis în loc de cache-ul în-memory, poți adăuga:

```csharp
builder.Services.AddOcelot(builder.Configuration)
    .AddCacheManager(options =>
    {
        options.WithRedisConfiguration("redis", redisConnectionString)
               .WithRedisCacheHandle("redis");
    });
```

Și în `ocelot.json`:
```json
"CacheOptions": {
  "TtlSeconds": 300,
  "Region": "books"
}
```

## Configurare Program.cs

Conform [documentației oficiale Ocelot](https://ocelot.readthedocs.io/en/latest/introduction/gettingstarted.html#net-core-3-1):

```csharp
// Ocelot Basic setup
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddOcelot(); // single ocelot.json file in read-only mode

builder.Services.AddOcelot(builder.Configuration);

// Build app
var app = builder.Build();

// Use Ocelot middleware (must await)
await app.UseOcelot();
await app.RunAsync();
```

**Puncte importante**:
- `AddOcelot()` adaugă fișierul `ocelot.json` în read-only mode
- `app.UseOcelot()` trebuie să fie `await`-at înainte de `app.RunAsync()`
- Nu folosi `app.MapGet()` etc. (minimal API) - Ocelot pipeline nu e compatibil
- `app.MapControllers()` și `app.MapRazorPages()` funcționează cu Ocelot

## Documentație

- [Ocelot Getting Started](https://ocelot.readthedocs.io/en/latest/introduction/gettingstarted.html#net-core-3-1) - Documentație oficială
- [Ocelot Caching](https://ocelot.readthedocs.io/en/latest/features/caching.html)
- [Ocelot Load Balancing](https://ocelot.readthedocs.io/en/latest/features/loadbalancer.html)

