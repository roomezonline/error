using ErrorService.Server.Data;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TechnicalController : ControllerBase
{
    private readonly ErrorServiceDbContext _context;
    private readonly IWebHostEnvironment _env;

    public TechnicalController(ErrorServiceDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private async Task<int?> ResolveSiteUserIdAsync()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(idStr) && int.TryParse(idStr, out var id))
            return id;

        var phone = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
            if (user != null) return user.Id;
        }
        return null;
    }

    // --- Error Codes ---

    [HttpGet("error-codes")]
    [OutputCache(Duration = 300, VaryByQueryKeys = new[] { "*" })]
    public async Task<ActionResult<IEnumerable<ErrorCode>>> GetErrorCodes(string? brand = null, string? deviceType = null, string? search = null)
    {
        var query = _context.ErrorCodes
            .Include(e => e.Documents)
            .AsQueryable();

        if (!string.IsNullOrEmpty(brand))
            query = query.Where(e => e.Brand == brand);

        if (!string.IsNullOrEmpty(deviceType))
            query = query.Where(e => e.DeviceType == deviceType);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(e => e.Code.Contains(search) || e.Description.Contains(search) || e.Solution.Contains(search));
        }

        return await query.ToListAsync();
    }

    [HttpGet("error-codes/recent")]
    [OutputCache(Duration = 600)]
    public async Task<ActionResult<IEnumerable<object>>> GetRecentErrorCodes([FromQuery] int take = 3)
    {
        return await _context.ErrorCodes
            .OrderByDescending(e => e.Id)
            .Take(take)
            .Select(e => new { e.Brand, e.DeviceType, e.Code, e.Description, e.Solution })
            .ToListAsync();
    }

    [HttpGet("error-codes/count")]
    [OutputCache(Duration = 600)]
    public async Task<ActionResult<int>> GetErrorCodeCount()
    {
        return await _context.ErrorCodes.CountAsync();
    }

    [HttpGet("error-codes/brands")]
    [OutputCache(Duration = 1800)]
    public async Task<ActionResult<IEnumerable<string>>> GetBrands()
    {
        return await _context.ErrorCodes.Select(e => e.Brand).Distinct().ToListAsync();
    }

    [HttpGet("error-codes/devices")]
    public async Task<ActionResult<IEnumerable<string>>> GetDeviceTypes()
    {
        return await _context.ErrorCodes.Select(e => e.DeviceType).Distinct().ToListAsync();
    }

    [HttpGet("error-codes/{id}")]
    [OutputCache(Duration = 600)]
    public async Task<ActionResult<ErrorCode>> GetErrorCodeById(int id)
    {
        var ec = await _context.ErrorCodes
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ec == null) return NotFound();
        return Ok(ec);
    }

    [HttpGet("error-codes/related/{id}")]
    [OutputCache(Duration = 600)]
    public async Task<ActionResult<IEnumerable<ErrorCode>>> GetRelatedErrorCodes(int id)
    {
        var current = await _context.ErrorCodes.FindAsync(id);
        if (current == null) return NotFound();

        var related = await _context.ErrorCodes
            .Include(e => e.Documents)
            .Where(e => e.Id != id && e.Brand == current.Brand && e.DeviceType == current.DeviceType)
            .OrderBy(e => e.Code)
            .Take(6)
            .ToListAsync();

        return Ok(related);
    }

    [HttpGet("error-codes/by-brand/{brand}")]
    [OutputCache(Duration = 600)]
    public async Task<ActionResult<IEnumerable<ErrorCode>>> GetErrorCodesByBrand(string brand)
    {
        return await _context.ErrorCodes
            .Include(e => e.Documents)
            .Where(e => e.Brand == brand)
            .OrderBy(e => e.DeviceType).ThenBy(e => e.Code)
            .ToListAsync();
    }

    [HttpGet("error-codes/by-device/{brand}/{deviceType}")]
    [OutputCache(Duration = 600)]
    public async Task<ActionResult<IEnumerable<ErrorCode>>> GetErrorCodesByDevice(string brand, string deviceType)
    {
        return await _context.ErrorCodes
            .Include(e => e.Documents)
            .Where(e => e.Brand == brand && e.DeviceType == deviceType)
            .OrderBy(e => e.Code)
            .ToListAsync();
    }

    [HttpPost("error-codes")]
    public async Task<ActionResult<ErrorCode>> PostErrorCode(ErrorCode errorCode)
    {
        _context.ErrorCodes.Add(errorCode);
        await _context.SaveChangesAsync();
        return Ok(errorCode);
    }

    [HttpPost("error-codes/upload")]
    public async Task<ActionResult<string>> UploadErrorCodeImage([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("فایل ارسال نشده است");

        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes)
            return BadRequest("حجم فایل زیاد است (حداکثر 5MB)");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var relativeFolder = "uploads/error-codes";
        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadsRoot = Path.Combine(wwwroot, relativeFolder);
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var outStream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(outStream);
        }

        return Ok($"/{relativeFolder}/{fileName}");
    }

    [HttpPut("error-codes/{id}")]
    public async Task<IActionResult> PutErrorCode(int id, ErrorCode errorCode)
    {
        if (id != errorCode.Id) return BadRequest();

        var existing = await _context.ErrorCodes
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (existing == null) return NotFound();

        existing.Brand = errorCode.Brand;
        existing.DeviceType = errorCode.DeviceType;
        existing.Code = errorCode.Code;
        existing.Description = errorCode.Description;
        existing.Solution = errorCode.Solution;
        existing.TechnicalNotes = errorCode.TechnicalNotes;
        existing.ModelNames = errorCode.ModelNames;
        existing.RelatedProductIds = errorCode.RelatedProductIds;
        existing.ImageUrl = errorCode.ImageUrl;

        _context.ErrorCodeDocuments.RemoveRange(existing.Documents);
        existing.Documents = (errorCode.Documents ?? new List<ErrorCodeDocument>())
            .OrderBy(d => d.SortOrder)
            .Select((d, i) => new ErrorCodeDocument
            {
                Title = d.Title,
                Url = d.Url,
                DocType = d.DocType,
                SortOrder = i
            })
            .ToList();

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("error-codes/import")]
    public async Task<ActionResult<ErrorCodeImportResult>> ImportErrorCodes(List<ErrorCodeImportModel> items)
    {
        var result = new ErrorCodeImportResult { TotalProcessed = items.Count };
        
        foreach (var item in items)
        {
            try
            {
                var existing = await _context.ErrorCodes
                    .Include(e => e.Documents)
                    .FirstOrDefaultAsync(e => e.Brand == item.Brand && e.DeviceType == item.DeviceType && e.Code == item.Code);

                if (existing != null)
                {
                    existing.Description = item.Description;
                    existing.Solution = item.Solution;
                    existing.TechnicalNotes = item.TechnicalNotes;
                    existing.ModelNames = item.ModelNames;

                    _context.ErrorCodeDocuments.RemoveRange(existing.Documents);
                    existing.Documents = item.Documents.Select((d, i) => new ErrorCodeDocument
                    {
                        Title = d.Title,
                        Url = d.Url,
                        DocType = d.DocType,
                        SortOrder = i
                    }).ToList();
                    
                    result.UpdatedCount++;
                }
                else
                {
                    var newCode = new ErrorCode
                    {
                        Brand = item.Brand,
                        DeviceType = item.DeviceType,
                        Code = item.Code,
                        Description = item.Description,
                        Solution = item.Solution,
                        TechnicalNotes = item.TechnicalNotes,
                        ModelNames = item.ModelNames,
                        Documents = item.Documents.Select((d, i) => new ErrorCodeDocument
                        {
                            Title = d.Title,
                            Url = d.Url,
                            DocType = d.DocType,
                            SortOrder = i
                        }).ToList()
                    };
                    _context.ErrorCodes.Add(newCode);
                    result.InsertedCount++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error processing {item.Brand} - {item.Code}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return Ok(result);
    }

    [HttpDelete("error-codes/{id}")]
    public async Task<IActionResult> DeleteErrorCode(int id)
    {
        var errorCode = await _context.ErrorCodes.FindAsync(id);
        if (errorCode == null) return NotFound();
        _context.ErrorCodes.Remove(errorCode);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // --- Consultation Tickets ---

    [HttpGet("tickets")]
    public async Task<ActionResult<IEnumerable<ConsultationTicket>>> GetTickets(string? userEmail = null)
    {
        // NOTE: This endpoint previously returned all tickets; now it is restricted.
        var query = _context.ConsultationTickets.Include(t => t.Replies).AsQueryable();

        if (!string.IsNullOrEmpty(userEmail))
        {
            query = query.Where(t => t.UserEmail == userEmail);
        }
        else
        {
            // If no userEmail is supplied, only allow authenticated users to see their own tickets.
            var userId = await ResolveSiteUserIdAsync();
            if (userId == null)
                return Unauthorized();

            query = query.Where(t => t.UserId == userId.Value);
        }
        
        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    [Authorize]
    [HttpGet("tickets/my")]
    public async Task<ActionResult<IEnumerable<ConsultationTicket>>> GetMyTickets()
    {
        var userId = await ResolveSiteUserIdAsync();
        if (userId == null)
            return Unauthorized();

        return await _context.ConsultationTickets
            .Include(t => t.Replies)
            .Where(t => t.UserId == userId.Value)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.consultation.manage")]
    [HttpGet("tickets/admin")]
    public async Task<ActionResult<IEnumerable<ConsultationTicket>>> GetAdminTickets()
    {
        var tickets = await _context.ConsultationTickets
            .Include(t => t.Replies)
            .OrderByDescending(t => t.IsAdminRead == false)
            .ThenByDescending(t => t.LastMessageAt)
            .ToListAsync();

        var userIds = tickets
            .Where(t => t.UserId.HasValue)
            .Select(t => t.UserId!.Value)
            .Distinct()
            .ToList();

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.PhoneNumber, u.Email })
            .ToDictionaryAsync(x => x.Id);

        foreach (var t in tickets)
        {
            if (t.UserId.HasValue && users.TryGetValue(t.UserId.Value, out var u))
            {
                t.UserFullName = u.FullName;
                t.UserPhoneNumber = u.PhoneNumber;
                t.UserEmail = u.Email;
            }
            else
            {
                t.UserFullName = null;
                t.UserPhoneNumber = null;
                t.UserEmail = null;
            }
        }

        return tickets;
    }

    [HttpPost("tickets/upload-attachment")]
    public async Task<ActionResult<dynamic>> UploadTicketAttachment([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("فایل ارسال نشده است");

        const long maxBytes = 10 * 1024 * 1024; // 10MB
        if (file.Length > maxBytes)
            return BadRequest("حجم فایل زیاد است (حداکثر 10MB)");

        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "tickets");
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var ext = Path.GetExtension(file.FileName);
        var originalName = file.FileName;
        var contentType = file.ContentType;

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new { 
            Url = $"/uploads/tickets/{fileName}", 
            Name = originalName, 
            ContentType = contentType 
        });
    }

    [HttpPost("tickets")]
    public async Task<ActionResult<ConsultationTicket>> PostTicket(ConsultationTicket ticket)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userIdStr) && int.TryParse(userIdStr, out var userId))
            ticket.UserId = userId;

        ticket.CreatedAt = DateTime.Now;
        ticket.LastMessageAt = DateTime.Now;
        ticket.IsAdminRead = false;
        ticket.IsUserRead = true;
        ticket.Status = TicketStatus.Pending;
        _context.ConsultationTickets.Add(ticket);
        await _context.SaveChangesAsync();
        return Ok(ticket);
    }

    [HttpPost("tickets/{id}/reply")]
    public async Task<ActionResult<TicketReply>> PostReply(int id, TicketReply reply)
    {
        var ticket = await _context.ConsultationTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        // If user is replying (not admin), ensure they own the ticket.
        if (!reply.IsAdmin)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            if (ticket.UserId.HasValue && ticket.UserId.Value != userId)
                return Forbid();
            
            ticket.IsAdminRead = false;
            ticket.IsUserRead = true;
        }
        else 
        {
            ticket.IsAdminRead = true;
            ticket.IsUserRead = false;
        }

        reply.RepliedAt = DateTime.Now;
        reply.ConsultationTicketId = ticket.Id;
        ticket.Replies.Add(reply);
        ticket.LastMessageAt = DateTime.Now;
        
        if (reply.IsAdmin)
            ticket.Status = TicketStatus.InProgress;

        await _context.SaveChangesAsync();
        return Ok(reply);
    }

    [HttpPost("tickets/{id}/read-admin")]
    public async Task<IActionResult> MarkAsReadAdmin(int id)
    {
        var ticket = await _context.ConsultationTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        ticket.IsAdminRead = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("tickets/{id}/read-user")]
    public async Task<IActionResult> MarkAsReadUser(int id)
    {
        var ticket = await _context.ConsultationTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        if (ticket.UserId != userId) return Forbid();

        ticket.IsUserRead = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("tickets/{id}/status")]
    public async Task<IActionResult> UpdateTicketStatus(int id, [FromBody] TicketStatus status)
    {
        var ticket = await _context.ConsultationTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        ticket.Status = status;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
