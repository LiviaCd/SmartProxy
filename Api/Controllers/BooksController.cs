using Api.Models;
using Api.Services;
using Cassandra;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using ZiggyCreatures.Caching.Fusion;
using Microsoft.Extensions.Logging;

namespace Api.Controllers;

[ApiController]
[Route("books")]
public class BooksController : ControllerBase
{
    private readonly CassandraService _cassandraService;
    private readonly IFusionCache _cache;
    private readonly ILogger<BooksController> _logger;

    public BooksController(CassandraService cassandraService, IFusionCache cache, ILogger<BooksController> logger)
    {
        _cassandraService = cassandraService;
        _cache = cache;
        _logger = logger;
    }

    private static string CacheKey(Guid id) => $"book:{id}";

    // CREATE
    [HttpPost]
    public IActionResult CreateBook([FromBody] BookCreateRequest request)
    {
        try
        {
            var bookId = Guid.NewGuid();

            _cassandraService.ExecuteWithFallback(
                "INSERT INTO books (id, title, author, year) VALUES (?, ?, ?, ?)",
                bookId, request.Title, request.Author, request.Year
            );

            _cache.SetAsync(CacheKey(bookId), new Book
            {
                Id = bookId,
                Title = request.Title,
                Author = request.Author,
                Year = request.Year
            });

            return Ok(new { id = bookId, message = "Book created" });
        }
        catch (UnavailableException)
        {
            return StatusCode(503, new
            {
                detail = "Database temporarily unavailable. Please try again later.",
                error = "Service Unavailable"
            });
        }
        catch
        {
            return StatusCode(500, new
            {
                detail = "An error occurred while creating the book.",
                error = "Internal Server Error"
            });
        }
    }

    // READ ALL
    [HttpGet]
    public IActionResult GetBooks()
    {
        try
        {
            var rows = _cassandraService.ExecuteWithFallback("SELECT * FROM books");

            var books = rows.Select(row => new Book
            {
                Id = row.GetValue<Guid>("id"),
                Title = row.GetValue<string>("title"),
                Author = row.GetValue<string>("author"),
                Year = row.GetValue<int>("year")
            }).ToList();

            return Ok(books);
        }
        catch (UnavailableException)
        {
            return StatusCode(503, new
            {
                detail = "Database temporarily unavailable. Please try again later.",
                error = "Service Unavailable"
            });
        }
        catch
        {
            return StatusCode(500, new
            {
                detail = "An error occurred while reading books.",
                error = "Internal Server Error"
            });
        }
    }

    // READ BY ID + CACHE
    [HttpGet("{bookId}")]
    public async Task<IActionResult> GetBook(Guid bookId)
    {
        try
        {
            var cacheKey = CacheKey(bookId);
            
            // Try to get from cache first
            var cachedBook = await _cache.TryGetAsync<Book>(cacheKey);
            if (cachedBook.HasValue)
            {
                _logger.LogInformation("[CACHE] ✅ HIT | Key:{CacheKey} | BookId:{BookId}", cacheKey, bookId);
                return Ok(cachedBook.Value);
            }

            // CACHE MISS - get from database
            _logger.LogInformation("[CACHE] ❌ MISS | Key:{CacheKey} | BookId:{BookId} | Fetching from database...", cacheKey, bookId);
            
            var book = await _cache.GetOrSetAsync(
                cacheKey,
                async _ =>
                {
                    _logger.LogInformation("[CACHE] 🔄 FETCHING | Key:{CacheKey} | BookId:{BookId} | Querying database...", cacheKey, bookId);
                    var row = _cassandraService.ExecuteWithFallback(
                        "SELECT * FROM books WHERE id = ?",
                        bookId
                    ).FirstOrDefault();

                    if (row == null)
                        return null;

                    return new Book
                    {
                        Id = row.GetValue<Guid>("id"),
                        Title = row.GetValue<string>("title"),
                        Author = row.GetValue<string>("author"),
                        Year = row.GetValue<int>("year")
                    };
                },
                TimeSpan.FromMinutes(5)
            );

            if (book == null)
                return NotFound(new { detail = "Book not found" });

            _logger.LogInformation("[CACHE] ✅ SET | Key:{CacheKey} | BookId:{BookId} | Cached for 5 minutes", cacheKey, bookId);
            return Ok(book);
        }
        catch (UnavailableException)
        {
            return StatusCode(503, new
            {
                detail = "Database temporarily unavailable. Please try later.",
                error = "Service Unavailable"
            });
        }
        catch
        {
            return StatusCode(500, new
            {
                detail = "An error occurred while reading the book.",
                error = "Internal Server Error"
            });
        }
    }

    // UPDATE
    [HttpPut("{bookId}")]
    public async Task<IActionResult> UpdateBook(Guid bookId, [FromBody] BookUpdateRequest request)
    {
        try
        {
            var existing = _cassandraService.ExecuteWithFallback(
                "SELECT id FROM books WHERE id = ?",
                bookId
            ).FirstOrDefault();

            if (existing == null)
                return NotFound(new { detail = "Book not found" });

            _cassandraService.ExecuteWithFallback(
                "UPDATE books SET title = ?, author = ?, year = ? WHERE id = ?",
                request.Title, request.Author, request.Year, bookId
            );

            // Update cache
            await _cache.SetAsync(CacheKey(bookId), new Book
            {
                Id = bookId,
                Title = request.Title,
                Author = request.Author,
                Year = request.Year
            });

            return Ok(new { message = "Book updated" });
        }
        catch (UnavailableException)
        {
            return StatusCode(503, new
            {
                detail = "Database temporarily unavailable. Please try again later.",
                error = "Service Unavailable"
            });
        }
        catch
        {
            return StatusCode(500, new
            {
                detail = "An error occurred while updating the book.",
                error = "Internal Server Error"
            });
        }
    }

    // DELETE
    [HttpDelete("{bookId}")]
    public async Task<IActionResult> DeleteBook(Guid bookId)
    {
        try
        {
            var existing = _cassandraService.ExecuteWithFallback(
                "SELECT id FROM books WHERE id = ?",
                bookId
            ).FirstOrDefault();

            if (existing == null)
                return NotFound(new { detail = "Book not found" });

            _cassandraService.ExecuteWithFallback(
                "DELETE FROM books WHERE id = ?",
                bookId
            );

            await _cache.RemoveAsync(CacheKey(bookId));

            return Ok(new { message = "Book deleted" });
        }
        catch (UnavailableException)
        {
            return StatusCode(503, new
            {
                detail = "Database temporarily unavailable. Please try again later.",
                error = "Service Unavailable"
            });
        }
    }
}
