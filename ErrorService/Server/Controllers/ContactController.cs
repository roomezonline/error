using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ContactController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public ContactController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] ContactMessageRequest request)
    {
        var msg = new ContactMessage
        {
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = request.Email?.Trim(),
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ContactMessages.Add(msg);
        await _db.SaveChangesAsync();

        return Ok(new { message = "پیام شما با موفقیت ثبت شد. در اسرع وقت با شما تماس خواهیم گرفت." });
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpGet]
    public async Task<ActionResult<List<ContactMessageDto>>> GetAll()
    {
        return await _db.ContactMessages
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ContactMessageDto
            {
                Id = x.Id,
                FullName = x.FullName,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                Subject = x.Subject,
                Message = x.Message,
                IsRead = x.IsRead,
                CreatedAtFa = x.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContactMessageDto>> GetById(int id)
    {
        var x = await _db.ContactMessages.FindAsync(id);
        if (x == null) return NotFound();

        if (!x.IsRead)
        {
            x.IsRead = true;
            await _db.SaveChangesAsync();
        }

        return new ContactMessageDto
        {
            Id = x.Id,
            FullName = x.FullName,
            PhoneNumber = x.PhoneNumber,
            Email = x.Email,
            Subject = x.Subject,
            Message = x.Message,
            IsRead = x.IsRead,
            CreatedAtFa = x.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
        };
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var msg = await _db.ContactMessages.FindAsync(id);
        if (msg == null) return NotFound();

        msg.IsRead = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var msg = await _db.ContactMessages.FindAsync(id);
        if (msg == null) return NotFound();

        _db.ContactMessages.Remove(msg);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
