# Arhitectura Proxy-ului - Explicație Detaliată

## Prezentare Generală

Proxy-ul este implementat ca un **Reverse Proxy** care acționează ca intermediar între clienți și serverele backend (API). Are trei funcționalități principale:

1. **Smart Proxy** - Forward-ează cererile către backend
2. **Load Balancing** - Distribuie cererile între multiple servere backend
3. **Caching** - Stochează răspunsurile pentru a accelera cererile viitoare

## Arhitectura Componentelor

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ HTTP Request
       ▼
┌─────────────────────────────────────┐
│   ReverseProxyMiddleware             │
│   (Middleware ASP.NET Core)         │
│                                      │
│   1. Verifică cache (Redis)         │
│   2. Load Balancing (Round-Robin)   │
│   3. Forward către backend          │
│   4. Cache răspunsul (dacă GET)     │
└──────┬──────────────────────────────┘
       │
       ├──► LoadBalancerService ──► Selectează următorul server
       │
       ├──► CacheService ──► Redis ──► Stochează/citește cache
       │
       └──► HttpClient ──► Forward către backend
                              │
                              ▼
                    ┌─────────────────┐
                    │  Backend API 1  │
                    │  Backend API 2  │
                    └─────────────────┘
```

## 1. ReverseProxyMiddleware - Inima Proxy-ului

### Poziționare în Pipeline

Middleware-ul este adăugat în `Program.cs` și se execută **înainte** de routing:

```csharp
app.UseMiddleware<ReverseProxyMiddleware>();
```

Aceasta înseamnă că interceptează **toate** cererile HTTP înainte ca acestea să ajungă la controller-e sau la fișiere statice.

### Fluxul unei Cereri

#### Pasul 1: Filtrare Cereri

```csharp
// Skip proxy pentru anumite rute
if (context.Request.Path.StartsWithSegments("/health") ||
    context.Request.Path.StartsWithSegments("/swagger") ||
    context.Request.Path.StartsWithSegments("/_") ||
    context.Request.Path.StartsWithSegments("/api"))
{
    await _next(context);  // Lasă cererea să treacă mai departe
    return;
}
```

**De ce?** 
- `/health` - endpoint propriu al proxy-ului
- `/swagger` - documentație API
- `/api` - endpoint-uri proprii (ex: `/api/cache/keys`)
- `/_` - resurse interne ASP.NET Core

#### Pasul 2: Verificare Cache (doar pentru GET)

```csharp
var cacheKey = _cacheService.GenerateCacheKey(method, path, queryString);

if (_enableCaching && method == "GET" && !string.IsNullOrEmpty(cacheKey))
{
    var cachedResponse = await _cacheService.GetAsync<CachedResponse>(cacheKey);
    if (cachedResponse != null)
    {
        // Cache HIT - returnează răspunsul din cache
        await WriteCachedResponse(context, cachedResponse);
        return;  // Nu mai forward-ează către backend!
    }
}
```

**Cheia cache-ului** este generată astfel:
- Format: `GET:/books` sau `GET:/books?id=1`
- Doar pentru metode GET (nu cache-uim POST/PUT/DELETE)

**Cache HIT**: Dacă găsește răspunsul în cache, îl returnează imediat clientului **fără** să contacteze backend-ul. Acest lucru:
- Reduce timpul de răspuns
- Reduce încărcarea pe backend
- Reduce traficul de rețea

#### Pasul 3: Load Balancing

```csharp
var backendServer = _loadBalancer.GetNextServer();
var targetUrl = $"{backendServer}{path}{queryString}";
```

**LoadBalancerService** selectează următorul server din listă folosind algoritmul **Round-Robin**.

#### Pasul 4: Forward Request către Backend

```csharp
var response = await ForwardRequest(context, targetUrl);
```

Metoda `ForwardRequest` face următoarele:

1. **Creează un HttpClient** (folosind `IHttpClientFactory` pentru pooling)
2. **Copiază headerele** din cererea originală (exceptând `Host` și `Connection`)
3. **Copiază body-ul** pentru POST/PUT/PATCH
4. **Trimite cererea** către backend
5. **Copiază răspunsul** înapoi către client:
   - Headerele răspunsului
   - Status code-ul
   - Body-ul răspunsului

#### Pasul 5: Cache Răspunsul (dacă e GET și succes)

```csharp
if (_enableCaching && method == "GET" && response.IsSuccessStatusCode && !string.IsNullOrEmpty(cacheKey))
{
    var responseBody = await response.Content.ReadAsStringAsync();
    var cachedResponse = new CachedResponse
    {
        StatusCode = (int)response.StatusCode,
        Headers = response.Headers.ToDictionary(...),
        Content = responseBody,
        ContentType = response.Content.Headers.ContentType?.ToString()
    };
    
    await _cacheService.SetAsync(cacheKey, cachedResponse);
}
```

**De ce doar GET?**
- GET este **idempotent** - nu modifică date
- POST/PUT/DELETE modifică date, deci cache-ul ar deveni invalid

#### Pasul 6: Invalidare Cache (pentru operații de scriere)

```csharp
if ((method == "POST" || method == "PUT" || method == "DELETE") && _enableCaching)
{
    await InvalidateRelatedCache(path);
}
```

**De ce invalidăm cache-ul?**
- Dacă adăugăm/modificăm/ștergem o carte, cache-ul pentru `/books` devine **stale** (învechit)
- Invalidăm atât resursa specifică, cât și lista (ex: dacă modificăm `/books/123`, invalidăm și `/books`)

## 2. LoadBalancerService - Distribuirea Cererilor

### Algoritm: Round-Robin

```csharp
public string GetNextServer()
{
    lock (_lockObject)  // Thread-safe
    {
        var server = _backendServers[_currentIndex];
        _currentIndex = (_currentIndex + 1) % _backendServers.Count;  // Circular
        return server;
    }
}
```

**Cum funcționează:**
1. Păstrează un index (`_currentIndex`) care indică serverul curent
2. Returnează serverul de la index-ul curent
3. Incrementează index-ul (cu wrap-around când ajunge la final)
4. Folosește `lock` pentru a fi **thread-safe** (multiple cereri simultane)

**Exemplu cu 2 servere:**
- Cererea 1 → `api1:8080`
- Cererea 2 → `api2:8080`
- Cererea 3 → `api1:8080` (din nou)
- Cererea 4 → `api2:8080`
- ...

**Avantaje:**
- Distribuie uniform încărcarea
- Simplu de implementat
- Funcționează bine când toate serverele au aceeași capacitate

## 3. CacheService - Gestionarea Cache-ului

### Stocare în Redis

```csharp
public async Task SetAsync<T>(string key, T value, int? expirationSeconds = null)
{
    var serializedValue = JsonSerializer.Serialize(value);
    var options = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(expirationSeconds ?? _cacheExpirationSeconds)
    };
    
    await _cache.SetStringAsync(key, serializedValue, options);
}
```

**Caracteristici:**
- **Serializare JSON**: Obiectele sunt serializate în JSON înainte de stocare
- **Expirare automată**: Cache-ul expiră după 5 minute (configurabil)
- **Distributed**: Redis permite cache distribuit între multiple instanțe de proxy

### Structura CachedResponse

```csharp
public class CachedResponse
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string Content { get; set; }
    public string ContentType { get; set; }
}
```

Stochează **tot** răspunsul pentru a putea reconstrui exact răspunsul original.

## 4. Configurare și Inițializare

### Program.cs - Setup

```csharp
// HttpClient pentru forward-are cereri
builder.Services.AddHttpClient();

// Redis pentru cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
});

// Servicii custom
builder.Services.AddSingleton<LoadBalancerService>();
builder.Services.AddSingleton<CacheService>();
```

**De ce Singleton?**
- `LoadBalancerService`: Păstrează starea (index-ul curent) între cereri
- `CacheService`: Wrapper peste `IDistributedCache` (care e deja singleton)

## Fluxul Complet al unei Cereri

### Exemplu: GET /books

```
1. Client → Proxy: GET /books
   │
2. ReverseProxyMiddleware.InvokeAsync()
   │
3. Verifică cache: "GET:/books" în Redis?
   │
   ├─ DA (Cache HIT):
   │  └─► Returnează răspuns din cache → Client
   │      (STOP - nu contactează backend)
   │
   └─ NU (Cache MISS):
      │
4. LoadBalancerService.GetNextServer()
   │  └─► Returnează: "http://api1:8080"
   │
5. ForwardRequest()
   │  ├─► Creează HttpClient
   │  ├─► Copiază headere
   │  ├─► Trimite: GET http://api1:8080/books
   │  └─► Primește răspuns
   │
6. Copiază răspunsul către client
   │
7. CacheService.SetAsync("GET:/books", răspuns)
   │  └─► Salvează în Redis cu expirare 5 min
   │
8. Client primește răspuns
```

### Exemplu: POST /books (creare carte)

```
1. Client → Proxy: POST /books {title: "Test", ...}
   │
2. ReverseProxyMiddleware.InvokeAsync()
   │  └─► Nu verifică cache (nu e GET)
   │
3. LoadBalancerService.GetNextServer()
   │  └─► Returnează: "http://api2:8080"
   │
4. ForwardRequest()
   │  ├─► Trimite: POST http://api2:8080/books
   │  └─► Primește răspuns (201 Created)
   │
5. Copiază răspunsul către client
   │
6. InvalidateRelatedCache("/books")
   │  ├─► Șterge "GET:/books" din Redis
   │  └─► Cache-ul devine invalid pentru următoarea cerere GET
   │
7. Client primește răspuns
```

## Avantaje ale Implementării

### 1. Performanță
- **Cache HIT**: Răspuns instant (fără contactare backend)
- **Load Balancing**: Distribuie încărcarea uniform

### 2. Scalabilitate
- Poți adăuga mai multe servere backend fără să modifici clientul
- Redis permite cache distribuit

### 3. Fiabilitate
- Error handling: Dacă un backend e down, returnează 502 Bad Gateway
- Thread-safe: Funcționează corect cu cereri simultane

### 4. Transparență
- Clientul nu știe că există proxy
- Headerele și body-ul sunt forward-ate complet

## Limitări și Îmbunătățiri Posibile

### Limitări Actuale:
1. **Round-Robin simplu**: Nu ține cont de încărcarea serverelor
2. **Cache invalidation simplă**: Invalidă doar rutele cunoscute
3. **Fără retry logic**: Dacă un backend e down, returnează eroare imediat

### Îmbunătățiri Posibile:
1. **Health checks**: Verifică dacă backend-urile sunt disponibile
2. **Weighted Round-Robin**: Servere cu capacitate diferită
3. **Cache headers**: Respectă `Cache-Control` din răspunsurile backend
4. **Circuit Breaker**: Oprește cererile către backend-uri care eșuează frecvent
5. **Rate Limiting**: Limitează numărul de cereri per client

## Concluzie

Proxy-ul este implementat ca un **middleware ASP.NET Core** care:
- Interceptează cererile HTTP
- Verifică cache-ul înainte de a contacta backend-ul
- Distribuie cererile între multiple servere
- Stochează răspunsurile pentru cereri viitoare
- Gestionează invalidarea cache-ului la modificări

Această implementare oferă un **reverse proxy funcțional** cu caching și load balancing, perfect pentru un sistem distribuit.

