using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ErrorService.Server.Data;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/pageviews")]
public class PageViewsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly ILogger<PageViewsController> _logger;

    public PageViewsController(ErrorServiceDbContext db, ILogger<PageViewsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("track")]
    public async Task<IActionResult> Track([FromBody] PageViewRequest request)
    {
        _logger.LogInformation("PageView Track called: EntityType={EntityType}, EntityId={EntityId}", request?.EntityType, request?.EntityId);

        if (string.IsNullOrWhiteSpace(request.EntityType) || request.EntityId <= 0)
        {
            _logger.LogWarning("PageView Track: Invalid parameters - EntityType={EntityType}, EntityId={EntityId}", request?.EntityType, request?.EntityId);
            return BadRequest("Invalid parameters");
        }

        try
        {
            switch (request.EntityType.ToLowerInvariant())
            {
                case "product":
                    var product = await _db.Products.FindAsync(request.EntityId);
                    if (product != null) { product.ViewCount++; await _db.SaveChangesAsync(); _logger.LogInformation("Product {Id} view count: {Count}", product.Id, product.ViewCount); }
                    else _logger.LogWarning("Product {Id} not found", request.EntityId);
                    break;

                case "course":
                    var course = await _db.TrainingCourses.FindAsync(request.EntityId);
                    if (course != null) { course.ViewCount++; await _db.SaveChangesAsync(); _logger.LogInformation("Course {Id} view count: {Count}", course.Id, course.ViewCount); }
                    else _logger.LogWarning("Course {Id} not found", request.EntityId);
                    break;

                case "lesson":
                    var lesson = await _db.TrainingLessons.FindAsync(request.EntityId);
                    if (lesson != null) { lesson.ViewCount++; await _db.SaveChangesAsync(); _logger.LogInformation("Lesson {Id} view count: {Count}", lesson.Id, lesson.ViewCount); }
                    else _logger.LogWarning("Lesson {Id} not found", request.EntityId);
                    break;

                case "article":
                    var article = await _db.TrainingArticles.FindAsync(request.EntityId);
                    if (article != null) { article.ViewCount++; await _db.SaveChangesAsync(); _logger.LogInformation("Article {Id} view count: {Count}", article.Id, article.ViewCount); }
                    else _logger.LogWarning("Article {Id} not found", request.EntityId);
                    break;

                default:
                    _logger.LogWarning("PageView Track: Unknown entity type: {EntityType}", request.EntityType);
                    return BadRequest("Unknown entity type");
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PageView Track error for {EntityType}/{EntityId}", request.EntityType, request.EntityId);
            return StatusCode(500);
        }
    }
}

public class PageViewRequest
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
}
