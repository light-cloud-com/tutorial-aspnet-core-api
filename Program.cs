using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapOpenApi();
// No UseHttpsRedirection: HTTPS ends at the Light Cloud edge, and the app
// itself is reached over plain HTTP inside the platform.

// In memory, so the list starts empty after every restart.
var bookmarks = new ConcurrentDictionary<int, Bookmark>();
var nextId = 0;

app.MapGet("/bookmarks", () => bookmarks.Values.OrderBy(b => b.Id));

app.MapGet("/bookmarks/{id:int}", (int id) =>
    bookmarks.TryGetValue(id, out var bookmark)
        ? Results.Ok(bookmark)
        : Results.NotFound(new { error = "Bookmark not found" }));

app.MapPost("/bookmarks", (NewBookmark input, ILogger<Program> logger) =>
{
    // Checks the [Required], [StringLength] and [Url] attributes on NewBookmark.
    var errors = new List<ValidationResult>();
    if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, validateAllProperties: true))
    {
        return Results.ValidationProblem(errors
            .GroupBy(e => e.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage ?? "").ToArray()));
    }

    var bookmark = new Bookmark(Interlocked.Increment(ref nextId), input.Title, input.Url);
    bookmarks[bookmark.Id] = bookmark;
    logger.LogInformation("Bookmark {Id} created for {Url}", bookmark.Id, bookmark.Url);
    return Results.Created($"/bookmarks/{bookmark.Id}", bookmark);
});

app.MapHealthChecks("/health");

app.Run();

record Bookmark(int Id, string Title, string Url);

record NewBookmark(
    [property: Required, StringLength(100)] string Title,
    [property: Required, Url] string Url);
