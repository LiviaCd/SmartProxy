using Api.Models;
using Api.Services;
using Cassandra;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("books")]
public class BooksController : ControllerBase
{
    private readonly CassandraService _cassandraService;
    private readonly ILogger<BooksController> _logger;

    public BooksController(CassandraService cassandraService, ILogger<BooksController> logger)
    {
        _cassandraService = cassandraService;
        _logger = logger;
    }

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

            _logger.LogInformation("[BOOKS] Created book: ID={BookId}, Title={Title}, Author={Author}", 
                bookId, request.Title, request.Author);
            return Ok(new { id = bookId, message = "Book created" });
        }
        catch (UnavailableException ex)
        {
            _logger.LogError(ex, "[BOOKS] CREATE failed - Cassandra unavailable");
            return StatusCode(503, new { 
                detail = "Database temporarily unavailable. Please try again later.",
                error = "Service Unavailable"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BOOKS] CREATE failed - Unexpected error");
            return StatusCode(500, new { 
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

            _logger.LogInformation("[BOOKS] READ ALL - Retrieved {Count} books", books.Count);
            return Ok(books);
        }
        catch (UnavailableException ex)
        {
            _logger.LogError(ex, "[BOOKS] READ ALL failed - Cassandra unavailable");
            return StatusCode(503, new { 
                detail = "Database temporarily unavailable. Please try again later.",
                error = "Service Unavailable"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BOOKS] READ ALL failed - Unexpected error");
            return StatusCode(500, new { 
                detail = "An error occurred while reading books.",
                error = "Internal Server Error"
            });
        }
    }

    // READ BY ID
    [HttpGet("{bookId}")]
    public IActionResult GetBook(Guid bookId)
    {
        try
        {
            var row = _cassandraService.ExecuteWithFallback(
                "SELECT * FROM books WHERE id = ?",
                bookId
            ).FirstOrDefault();

            if (row == null)
            {
                _logger.LogWarning("[BOOKS] READ BY ID - Book not found: ID={BookId}", bookId);
                return NotFound(new { detail = "Book not found" });
            }

            var book = new Book
            {
                Id = row.GetValue<Guid>("id"),
                Title = row.GetValue<string>("title"),
                Author = row.GetValue<string>("author"),
                Year = row.GetValue<int>("year")
            };

            _logger.LogInformation("[BOOKS] READ BY ID - Retrieved book: ID={BookId}, Title={Title}", 
                bookId, book.Title);
            return Ok(book);
        }
        catch (UnavailableException ex)
        {
            _logger.LogError(ex, "[BOOKS] READ BY ID failed - Cassandra unavailable: ID={BookId}", bookId);
            return StatusCode(503, new { 
                detail = "Database temporarily unavailable. Please try again later.",
                error = "Service Unavailable"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BOOKS] READ BY ID failed - Unexpected error: ID={BookId}", bookId);
            return StatusCode(500, new { 
                detail = "An error occurred while reading the book.",
                error = "Internal Server Error"
            });
        }
    }

    // UPDATE
    [HttpPut("{bookId}")]
    public IActionResult UpdateBook(Guid bookId, [FromBody] BookUpdateRequest request)
    {
        try
        {
            // Check if book exists
            var existingRow = _cassandraService.ExecuteWithFallback(
                "SELECT id FROM books WHERE id = ?",
                bookId
            ).FirstOrDefault();

            if (existingRow == null)
            {
                _logger.LogWarning("[BOOKS] UPDATE - Book not found: ID={BookId}", bookId);
                return NotFound(new { detail = "Book not found" });
            }

            // Update book
            _cassandraService.ExecuteWithFallback(
                "UPDATE books SET title = ?, author = ?, year = ? WHERE id = ?",
                request.Title, request.Author, request.Year, bookId
            );

            _logger.LogInformation("[BOOKS] UPDATE - Updated book: ID={BookId}, Title={Title}, Author={Author}", 
                bookId, request.Title, request.Author);
            return Ok(new { message = "Book updated" });
        }
        catch (UnavailableException ex)
        {
            _logger.LogError(ex, "[BOOKS] UPDATE failed - Cassandra unavailable: ID={BookId}", bookId);
            return StatusCode(503, new { 
                detail = "Database temporarily unavailable. Please try again later.",
                error = "Service Unavailable"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BOOKS] UPDATE failed - Unexpected error: ID={BookId}", bookId);
            return StatusCode(500, new { 
                detail = "An error occurred while updating the book.",
                error = "Internal Server Error"
            });
        }
    }

    // DELETE
    [HttpDelete("{bookId}")]
    public IActionResult DeleteBook(Guid bookId)
    {
        try
        {
            // Check if book exists
            var existingRow = _cassandraService.ExecuteWithFallback(
                "SELECT id FROM books WHERE id = ?",
                bookId
            ).FirstOrDefault();

            if (existingRow == null)
            {
                _logger.LogWarning("[BOOKS] DELETE - Book not found: ID={BookId}", bookId);
                return NotFound(new { detail = "Book not found" });
            }

            // Delete book
            _cassandraService.ExecuteWithFallback(
                "DELETE FROM books WHERE id = ?",
                bookId
            );

            _logger.LogInformation("[BOOKS] DELETE - Deleted book: ID={BookId}", bookId);
            return Ok(new { message = "Book deleted" });
        }
        catch (UnavailableException ex)
        {
            _logger.LogError(ex, "[BOOKS] DELETE failed - Cassandra unavailable: ID={BookId}", bookId);
            return StatusCode(503, new { 
                detail = "Database temporarily unavailable. Please try again later.",
                error = "Service Unavailable"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BOOKS] DELETE failed - Unexpected error: ID={BookId}", bookId);
            return StatusCode(500, new { 
                detail = "An error occurred while deleting the book.",
                error = "Internal Server Error"
            });
        }
    }
}

