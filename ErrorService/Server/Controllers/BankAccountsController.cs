using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BankAccountsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public BankAccountsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<BankAccountDto>>> GetAll([FromQuery] int? workshopId = null, bool onlyActive = false)
    {
        var q = _db.BankAccounts.AsQueryable();

        if (workshopId is > 0)
            q = q.Where(x => x.WorkshopId == workshopId.Value);
        else if (!User.IsInRole("super_admin"))
            q = q.Where(x => x.WorkshopId == ClaimsHelper.GetWorkshopId(User));

        if (onlyActive) q = q.Where(x => x.IsActive);

        var list = await q
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.Id)
            .Select(x => new BankAccountDto
            {
                Id = x.Id,
                WorkshopId = x.WorkshopId,
                Title = x.Title,
                BankName = x.BankName,
                OwnerName = x.OwnerName,
                CardNumber = x.CardNumber,
                AccountNumber = x.AccountNumber,
                Iban = x.Iban,
                IconUrl = x.IconUrl,
                ShowInGateway = x.ShowInGateway,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("gateway")]
    public async Task<ActionResult<List<BankAccountDto>>> GetForGateway()
    {
        var list = await _db.BankAccounts
            .Where(x => x.WorkshopId == 1 && x.IsActive && x.ShowInGateway)
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.Id)
            .Select(x => new BankAccountDto
            {
                Id = x.Id,
                WorkshopId = x.WorkshopId,
                Title = x.Title,
                BankName = x.BankName,
                OwnerName = x.OwnerName,
                CardNumber = x.CardNumber,
                AccountNumber = x.AccountNumber,
                Iban = x.Iban,
                IconUrl = x.IconUrl,
                ShowInGateway = x.ShowInGateway,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BankAccountDto>> GetById(int id)
    {
        var x = await _db.BankAccounts.FirstOrDefaultAsync(a => a.Id == id);
        if (x is null) return NotFound();

        return Ok(new BankAccountDto
        {
            Id = x.Id,
            WorkshopId = x.WorkshopId,
            Title = x.Title,
            BankName = x.BankName,
            OwnerName = x.OwnerName,
            CardNumber = x.CardNumber,
            AccountNumber = x.AccountNumber,
            Iban = x.Iban,
            IconUrl = x.IconUrl,
            ShowInGateway = x.ShowInGateway,
            IsActive = x.IsActive,
            SortOrder = x.SortOrder
        });
    }

    [Authorize(Policy = "perm:admin.bankaccounts.manage")]
    [HttpPost]
    public async Task<ActionResult<BankAccountDto>> Create([FromBody] BankAccountUpsertRequest request)
    {
        var workshopId = request.WorkshopId;
        if (!User.IsInRole("super_admin"))
            workshopId = ClaimsHelper.GetWorkshopId(User);

        var now = DateTimeOffset.UtcNow;
        var x = new BankAccount
        {
            WorkshopId = workshopId,
            Title = request.Title,
            BankName = request.BankName,
            OwnerName = request.OwnerName,
            CardNumber = request.CardNumber,
            AccountNumber = request.AccountNumber,
            Iban = request.Iban,
            IconUrl = request.IconUrl,
            ShowInGateway = request.ShowInGateway,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.BankAccounts.Add(x);
        await _db.SaveChangesAsync();

        return Ok(new BankAccountDto { Id = x.Id, WorkshopId = x.WorkshopId, Title = x.Title });
    }

    [Authorize(Policy = "perm:admin.bankaccounts.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] BankAccountUpsertRequest request)
    {
        var x = await _db.BankAccounts.FirstOrDefaultAsync(a => a.Id == id);
        if (x is null) return NotFound();

        var workshopId = request.WorkshopId;
        if (!User.IsInRole("super_admin"))
            workshopId = ClaimsHelper.GetWorkshopId(User);

        x.WorkshopId = workshopId;
        x.Title = request.Title;
        x.BankName = request.BankName;
        x.OwnerName = request.OwnerName;
        x.CardNumber = request.CardNumber;
        x.AccountNumber = request.AccountNumber;
        x.Iban = request.Iban;
        x.IconUrl = request.IconUrl;
        x.ShowInGateway = request.ShowInGateway;
        x.IsActive = request.IsActive;
        x.SortOrder = request.SortOrder;
        x.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.bankaccounts.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var x = await _db.BankAccounts.FirstOrDefaultAsync(a => a.Id == id);
        if (x is null) return NotFound();

        _db.BankAccounts.Remove(x);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
