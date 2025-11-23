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
        var bookId = Guid.NewGuid();
        
        var statement = _cassandraService.CreateStatement(
            "INSERT INTO books (id, title, author, year) VALUES (?, ?, ?, ?)",
            bookId, request.Title, request.Author, request.Year
        );

        _cassandraService.Session.Execute(statement);

        return Ok(new { id = bookId, message = "Book created" });
    }

    // READ ALL
    [HttpGet]
    public IActionResult GetBooks()
    {
        var statement = _cassandraService.CreateStatement("SELECT * FROM books");
        var rows = _cassandraService.Session.Execute(statement);
        
        var books = rows.Select(row => new Book
        {
            Id = row.GetValue<Guid>("id"),
            Title = row.GetValue<string>("title"),
            Author = row.GetValue<string>("author"),
            Year = row.GetValue<int>("year")
        }).ToList();

        return Ok(books);
    }

    // READ BY ID
    [HttpGet("{bookId}")]
    public IActionResult GetBook(Guid bookId)
    {
        var statement = _cassandraService.CreateStatement(
            "SELECT * FROM books WHERE id = ?",
            bookId
        );

        var row = _cassandraService.Session.Execute(statement).FirstOrDefault();

        if (row == null)
        {
            return NotFound(new { detail = "Book not found" });
        }

        var book = new Book
        {
            Id = row.GetValue<Guid>("id"),
            Title = row.GetValue<string>("title"),
            Author = row.GetValue<string>("author"),
            Year = row.GetValue<int>("year")
        };

        return Ok(book);
    }

    // UPDATE
    [HttpPut("{bookId}")]
    public IActionResult UpdateBook(Guid bookId, [FromBody] BookUpdateRequest request)
    {
        // Check if book exists
        var checkStatement = _cassandraService.CreateStatement(
            "SELECT id FROM books WHERE id = ?",
            bookId
        );

        var existingRow = _cassandraService.Session.Execute(checkStatement).FirstOrDefault();

        if (existingRow == null)
        {
            return NotFound(new { detail = "Book not found" });
        }

        // Update book
        var updateStatement = _cassandraService.CreateStatement(
            "UPDATE books SET title = ?, author = ?, year = ? WHERE id = ?",
            request.Title, request.Author, request.Year, bookId
        );

        _cassandraService.Session.Execute(updateStatement);

        return Ok(new { message = "Book updated" });
    }

    // DELETE
    [HttpDelete("{bookId}")]
    public IActionResult DeleteBook(Guid bookId)
    {
        // Check if book exists
        var checkStatement = _cassandraService.CreateStatement(
            "SELECT id FROM books WHERE id = ?",
            bookId
        );

        var existingRow = _cassandraService.Session.Execute(checkStatement).FirstOrDefault();

        if (existingRow == null)
        {
            return NotFound(new { detail = "Book not found" });
        }

        // Delete book
        var deleteStatement = _cassandraService.CreateStatement(
            "DELETE FROM books WHERE id = ?",
            bookId
        );

        _cassandraService.Session.Execute(deleteStatement);

        return Ok(new { message = "Book deleted" });
    }
}

