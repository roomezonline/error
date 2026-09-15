using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ErrorService.Server.Data;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/pageviews")]
public class PageViewsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public PageViewsController(ErrorServiceDbContext db) => _db = db;

    [HttpPost("track")]
    public async Task<IActionResult> Track([FromBody] PageViewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EntityType) || request.EntityId <= 0)
            return BadRequest("Invalid parameters");

        try
        {
            switch (request.EntityType.ToLowerInvariant())
            {
                case "product":
                    var product = await _db.Products.FindAsync(request.EntityId);
                    if (product != null) { product.ViewCount++; await _db.SaveChangesAsync(); }
                    break;

                case "course":
                    var course = await _db.TrainingCourses.FindAsync(request.EntityId);
                    if (course != null) { course.ViewCount++; await _db.SaveChangesAsync(); }
                    break;

                case "lesson":
                    var lesson = await _db.TrainingLessons.FindAsync(request.EntityId);
                    if (lesson != null) { lesson.ViewCount++; await _db.SaveChangesAsync(); }
                    break;

                case "article":
                    var article = await _db.TrainingArticles.FindAsync(request.EntityId);
                    if (article != null) { article.ViewCount++; await _db.SaveChangesAsync(); }
                    break;

                default:
                    return BadRequest("Unknown entity type");
            }

            return Ok();
        }
        catch
        {
            return StatusCode(500);
        }
    }
}

public class PageViewRequest
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
}
