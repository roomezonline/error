using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErrorService.Shared;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrintController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;


    public class PrintPreviewResponse
    {
        public string Html { get; set; } = "";
        public string Title { get; set; } = "";
        public int? ReceiptId { get; set; }
        public string CustomerName { get; set; } = "";
    }

    public PrintController(ErrorServiceDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet("settings")]
    [Authorize]
    public async Task<ActionResult<PrintSettingsDto>> GetPrintSettings()
    {
        var settings = await _db.PrintSettings.FirstOrDefaultAsync();
        if (settings != null)
        {
            return Ok(new PrintSettingsDto
            {
                HeaderTitle = settings.HeaderTitle,
                WorkshopName = settings.WorkshopName,
                ShowPrintDate = settings.ShowPrintDate,
                ShowInvoiceNumber = settings.ShowInvoiceNumber,
                Address = settings.Address,
                Phone = settings.Phone,
                Website = settings.Website,
                LogoUrl = settings.LogoUrl
            });
        }
        return Ok(new PrintSettingsDto());
    }

    [HttpPost("settings")]
    [Authorize]
    public async Task<ActionResult> UpsertPrintSettings([FromBody] PrintSettingsUpsertRequest request)
    {
        var settings = await _db.PrintSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new PrintSettings();
            _db.PrintSettings.Add(settings);
        }

        settings.HeaderTitle = request.HeaderTitle;
        settings.WorkshopName = request.WorkshopName;
        settings.ShowPrintDate = request.ShowPrintDate;
        settings.ShowInvoiceNumber = request.ShowInvoiceNumber;
        settings.Address = request.Address;
        settings.Phone = request.Phone;
        settings.Website = request.Website;
        settings.LogoUrl = request.LogoUrl;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("upload-logo")]
    [Authorize]
    public async Task<ActionResult<string>> UploadLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("فایل انتخاب نشده است");

        try
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "print-logos");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/print-logos/{uniqueFileName}";
            return Ok(url);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"خطا در آپلود فایل: {ex.Message}");
        }
    }

    [HttpPost("invoice/single")]
    [Authorize]
    public async Task<ActionResult<PrintPreviewResponse>> PrintSingleInvoice([FromBody] PrintInvoiceRequest request)
    {
        try
        {
            var receipt = await _db.CustomerReceipts
                .Include(r => r.Customer)
                .Include(r => r.DeviceType)
                .Include(r => r.DeviceBrand)
                .Include(r => r.Billing)
                .ThenInclude(b => b.Items)
                .FirstOrDefaultAsync(r => r.Id == request.ReceiptId);

            if (receipt == null)
                return NotFound("رسید یافت نشد");

            var html = GenerateInvoiceHtml(receipt, receipt.Billing, request);

            return Ok(new PrintPreviewResponse
            {
                Html = html,
                Title = $"فاکتور {receipt.Id}",
                ReceiptId = receipt.Id,
                CustomerName = $"{receipt.Customer.FirstName} {receipt.Customer.LastName}"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"خطا در تولید فاکتور: {ex.Message}");
        }
    }

    [HttpPost("invoice/bulk")]
    [Authorize]
    public async Task<ActionResult<PrintPreviewResponse>> PrintBulkInvoices([FromBody] BulkPrintInvoiceRequest request)
    {
        try
        {
            var receipts = await _db.CustomerReceipts
                .Include(r => r.Customer)
                .Include(r => r.DeviceType)
                .Include(r => r.DeviceBrand)
                .Include(r => r.Billing)
                .ThenInclude(b => b.Items)
                .Where(r => r.Customer.Mobile == request.CustomerMobile)
                .OrderByDescending(r => r.RegisteredAt)
                .ToListAsync();

            if (!receipts.Any())
                return NotFound("رسیدی برای این مشتری یافت نشد");

            var html = GenerateBulkInvoiceHtml(receipts, request);
            var firstReceipt = receipts.First();

            return Ok(new PrintPreviewResponse
            {
                Html = html,
                Title = $"گزارش فاکتورهای {firstReceipt.Customer.FirstName} {firstReceipt.Customer.LastName}",
                ReceiptId = null,
                CustomerName = $"{firstReceipt.Customer.FirstName} {firstReceipt.Customer.LastName}"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"خطا در تولید فاکتورها: {ex.Message}");
        }
    }

    private string GenerateInvoiceHtml(CustomerReceipt receipt, CustomerReceiptBilling? billing, PrintInvoiceRequest settings)
    {
        var htmlWriter = new StringWriter();

        var workshopName = !string.IsNullOrEmpty(settings.WorkshopName) ? settings.WorkshopName : "";
        var headerTitle = !string.IsNullOrEmpty(settings.HeaderTitle) ? settings.HeaderTitle : "فاکتور رسمی";
        var address = !string.IsNullOrEmpty(settings.Address) ? settings.Address : "";
        var phone = !string.IsNullOrEmpty(settings.Phone) ? settings.Phone : "";
        var website = !string.IsNullOrEmpty(settings.Website) ? settings.Website : "";

        var regDate = receipt.RegisteredAt.ToString("yyyy/MM/dd HH:mm");
        var printDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
        var invoiceNumber = receipt.Id.ToString("D8");

        var total = billing?.BillTotal ?? 0;
        var discount = billing?.Discount ?? 0;
        var payable = total - discount;
        var paid = (billing?.Prepaid ?? 0) + (billing?.Paid ?? 0);
        var remaining = payable - paid;

        var logoHtml = !string.IsNullOrEmpty(settings.LogoUrl)
            ? $"<img src=\"{settings.LogoUrl}\" class=\"logo-img\" alt=\"logo\">"
            : "";

        var statusClass = receipt.Status switch
        {
            "pending" => "badge-warning",
            "repaired" => "badge-success",
            "delivered" => "badge-info",
            "not_repairable" => "badge-danger",
            _ => "badge-secondary"
        };

        var statusText = receipt.Status switch
        {
            "pending" => "در انتظار تعمیر",
            "repaired" => "تعمیر شده",
            "delivered" => "تحویل شده",
            "not_repairable" => "غیر قابل تعمیر",
            _ => receipt.Status
        };

        // جدول آیتم‌ها - ترتیب صحیح از راست: ردیف | شرح | مبلغ
        var itemsHtml = "";
        if (billing != null && billing.Items.Any())
        {
            var itemsHtmlWriter = new StringWriter();
            int index = 1;
            foreach (var item in billing.Items)
            {
                itemsHtmlWriter.WriteLine($@"
                    <tr class=""item-row"">
                        <td class=""col-no"">{index++}</td>
                        <td class=""col-desc"">{item.Title}</td>
                        <td class=""col-price"">{item.UnitPrice.ToString("N0")} ریال</td>
                    </tr>");
            }

            itemsHtml = $@"
            <div class=""items-section"">
                <table class=""items-table"">
                    <thead>
                        <tr>
                            <th class=""col-no"">#</th>
                            <th class=""col-desc"">شرح خدمات / قطعات</th>
                            <th class=""col-price"">مبلغ (ریال)</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemsHtmlWriter.ToString()}
                    </tbody>
                </table>
            </div>";
        }
        else
        {
            itemsHtml = "<div class=\"empty-items\">هیچ آیتمی برای این فاکتور ثبت نشده است</div>";
        }

        htmlWriter.WriteLine($@"
<!DOCTYPE html>
<html lang=""fa"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>فاکتور {invoiceNumber}</title>
    <style>
        @font-face {{
            font-family: 'Vazirmatn';
            font-weight: 400;
            src: url('/fonts/vazirmatn/Vazirmatn-Regular.woff2') format('woff2');
            font-display: swap;
        }}
        @font-face {{
            font-family: 'Vazirmatn';
            font-weight: 700;
            src: url('/fonts/vazirmatn/Vazirmatn-Bold.woff2') format('woff2');
            font-display: swap;
        }}
        
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: 'Vazirmatn', 'Tahoma', 'Arial', sans-serif;
            background: #eef2f7;
            padding: 20px;
            direction: rtl;
        }}
        
        .invoice-wrapper {{
            max-width: 850px;
            margin: 0 auto;
            background: #fff;
            border-radius: 12px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
            overflow: hidden;
            border: 1px solid #e2e8f0;
        }}
        
        .invoice-header {{
            background: #fff;
            padding: 20px 28px 16px;
            border-bottom: 2px solid #e2e8f0;
        }}
        
        .header-main {{
            text-align: center;
            margin-bottom: 12px;
        }}
        
        .invoice-title {{
            font-size: 1.6rem;
            font-weight: 700;
            color: #1a2c3e;
            letter-spacing: 1px;
            margin-bottom: 6px;
        }}
        
        .workshop-name {{
            font-size: 0.85rem;
            color: #7f8c8d;
            font-weight: 400;
        }}
        
        .header-divider {{
            height: 1px;
            background: #ddd;
            margin: 12px 0;
        }}
        
        .header-meta {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            flex-wrap: wrap;
            gap: 10px;
        }}
        
        .logo-area {{
            flex-shrink: 0;
        }}
        
        .logo-img {{
            max-height: 55px;
            max-width: 150px;
            object-fit: contain;
        }}
        
        .meta-numbers {{
            text-align: right;
            font-size: 0.75rem;
            color: #555;
            line-height: 1.6;
        }}
        
        .meta-numbers div {{
            margin: 2px 0;
        }}
        
        .status-badge-row {{
            display: flex;
            justify-content: center;
            gap: 20px;
            margin-top: 12px;
            flex-wrap: wrap;
        }}
        
        .badge {{
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 5px 16px;
            border-radius: 20px;
            font-size: 0.75rem;
            font-weight: 600;
            border: 1px solid;
        }}
        
        .badge-warning {{ background: #fef3c7; color: #d97706; border-color: #fde68a; }}
        .badge-success {{ background: #d1fae5; color: #059669; border-color: #a7f3d0; }}
        .badge-info {{ background: #dbeafe; color: #2563eb; border-color: #bfdbfe; }}
        .badge-danger {{ background: #fee2e2; color: #dc2626; border-color: #fecaca; }}
        
        .invoice-body {{
            padding: 20px 28px;
        }}
        
        /* دو باکس کنار هم */
        .info-double-box {{
            display: flex;
            gap: 20px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }}
        
        .info-box {{
            flex: 1;
            background: #f8fafc;
            border-radius: 14px;
            border: 1px solid #e2e8f0;
            overflow: hidden;
        }}
        
        .info-box-header {{
            background: #f1f5f9;
            padding: 12px 16px;
            border-bottom: 1px solid #e2e8f0;
            display: flex;
            align-items: center;
            gap: 8px;
        }}
        
        .info-box-header h3 {{
            font-size: 0.9rem;
            font-weight: 700;
            color: #2c3e50;
            margin: 0;
        }}
        
        .info-box-content {{
            padding: 14px 16px;
        }}
        
        .info-row {{
            display: flex;
            align-items: baseline;
            gap: 8px;
            margin-bottom: 10px;
            flex-wrap: wrap;
        }}
        
        .info-row:last-child {{
            margin-bottom: 0;
        }}
        
        .info-label {{
            font-size: 0.75rem;
            color: #7f8c8d;
            font-weight: 500;
            min-width: 55px;
        }}
        
        .info-value {{
            font-size: 0.85rem;
            color: #2c3e50;
            font-weight: 500;
        }}
        
        .device-spec {{
            background: #f1f5f9;
            border-radius: 10px;
            padding: 12px;
            margin-bottom: 12px;
        }}
        
        .problem-box {{
            background: #fefce8;
            border-radius: 10px;
            padding: 12px;
            border-right: 4px solid #f59e0b;
        }}
        
        .problem-label {{
            font-size: 0.7rem;
            color: #d97706;
            font-weight: 600;
            margin-bottom: 6px;
        }}
        
        .problem-text {{
            font-size: 0.85rem;
            color: #334155;
            line-height: 1.5;
        }}
        
        .items-section {{
            margin: 20px 0;
        }}
        
        .items-table {{
            width: 100%;
            border-collapse: collapse;
            font-size: 0.8rem;
        }}
        
        .items-table th {{
            background: #f1f5f9;
            padding: 10px 12px;
            text-align: center;
            font-weight: 700;
            color: #2c3e50;
            border: 1px solid #cbd5e1;
        }}
        
        .items-table td {{
            padding: 5px 12px;
            border: 1px solid #e2e8f0;
            vertical-align: middle;
        }}
        
        .col-no {{
            width: 45px;
            text-align: center;
        }}
        
        .col-desc {{
            text-align: right;
        }}
        
        .col-price {{
            width: 130px;
            text-align: left;
            font-weight: 600;
        }}
        
        .summary-box {{
            background: #f8fafc;
            border-radius: 14px;
            padding: 14px 20px;
            margin-top: 20px;
            border: 1px solid #e2e8f0;
        }}
        
        .summary-row {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 8px 0;
            border-bottom: 1px solid #e2e8f0;
        }}
        
        .summary-row:last-child {{
            border-bottom: none;
        }}
        
        .summary-label {{
            font-size: 0.8rem;
            color: #666;
            font-weight: 500;
        }}
        
        .summary-value {{
            font-size: 0.85rem;
            font-weight: 700;
            text-align: left;
        }}
        
        .total-row {{
            background: #eef2ff;
            margin: 0 -20px;
            padding: 10px 20px;
            border-radius: 10px;
        }}
        
        .total-row .summary-label {{
            color: #1e3c72;
            font-weight: 700;
        }}
        
        .total-row .summary-value {{
            color: #2563eb;
            font-size: 0.95rem;
        }}
        
        .discount-value {{ color: #dc2626; }}
        .paid-value {{ color: #059669; }}
        .remaining-debtor {{ color: #dc2626; font-weight: 700; }}
        .remaining-creditor {{ color: #059669; font-weight: 700; }}
        
        .invoice-footer {{
            background: #f8fafc;
            padding: 15px 28px;
            text-align: center;
            border-top: 2px solid #e2e8f0;
        }}
        
        .footer-contact {{
            display: flex;
            justify-content: center;
            gap: 25px;
            flex-wrap: wrap;
            margin-bottom: 8px;
        }}
        
        .contact-item {{
            font-size: 0.7rem;
            color: #666;
            display: inline-flex;
            align-items: center;
            gap: 5px;
        }}
        
        .copyright {{
            font-size: 0.65rem;
            color: #888;
        }}
        
        .empty-items {{
            text-align: center;
            padding: 25px;
            color: #94a3b8;
            background: #f8fafc;
            border-radius: 10px;
            font-size: 0.8rem;
        }}
        
        @media print {{
            body {{ background: white; padding: 0; }}
            .invoice-wrapper {{ box-shadow: none; border: 1px solid #ccc; }}
        }}
        
        @media (max-width: 650px) {{
            .invoice-header, .invoice-body {{ padding: 15px; }}
            .header-meta {{ flex-direction: column; align-items: center; text-align: center; }}
            .meta-numbers {{ text-align: center; }}
            .info-double-box {{ flex-direction: column; gap: 12px; }}
            .col-price {{ width: 100px; }}
            .footer-contact {{ flex-direction: column; gap: 5px; }}
        }}
    </style>
</head>
<body>
    <div class=""invoice-wrapper"">
        <div class=""invoice-header"">
            <div class=""header-main"">
                <div class=""invoice-title"">{headerTitle}</div>
                {(!string.IsNullOrEmpty(workshopName) ? $"<div class=\"workshop-name\">{workshopName}</div>" : "")}
            </div>
            <div class=""header-divider""></div>
            <div class=""header-meta"">
                <div class=""logo-area"">
                    {logoHtml}
                </div>
                <div class=""meta-numbers"">
                    {(settings.ShowInvoiceNumber ? $"<div>شماره فاکتور: {invoiceNumber}</div>" : "")}
                    {(settings.ShowPrintDate ? $"<div>تاریخ چاپ: {printDate}</div>" : "")}
                    <div>تاریخ ثبت: {regDate}</div>
                </div>
            </div>
            <div class=""status-badge-row"">
                <span class=""badge {statusClass}"">{statusText}</span>
                <span class=""badge"">کد پیگیری: {receipt.Id}</span>
            </div>
        </div>
        
        <div class=""invoice-body"">
            <div class=""info-double-box"">
                <div class=""info-box"">
                    <div class=""info-box-header"">
                        <span>👤</span>
                        <h3>اطلاعات مشتری</h3>
                    </div>
                    <div class=""info-box-content"">
                        <div class=""info-row"">
                            <span class=""info-label"">نام:</span>
                            <span class=""info-value"">{receipt.Customer.FirstName} {receipt.Customer.LastName}</span>
                        </div>
                        <div class=""info-row"">
                            <span class=""info-label"">تلفن:</span>
                            <span class=""info-value ltr"">{receipt.Customer.Mobile}</span>
                        </div>
                        {(string.IsNullOrEmpty(receipt.Customer.Address) ? "" : $@"
                        <div class=""info-row"">
                            <span class=""info-label"">آدرس:</span>
                            <span class=""info-value"">{receipt.Customer.Address}</span>
                        </div>
                        ")}
                    </div>
                </div>
                
                <div class=""info-box"">
                    <div class=""info-box-header"">
                        <span>🖥️</span>
                        <h3>اطلاعات دستگاه</h3>
                    </div>
                    <div class=""info-box-content"">
                        <div class=""device-spec"">
                            <div class=""info-row"">
                                <span class=""info-label"">نوع دستگاه:</span>
                                <span class=""info-value"">{receipt.DeviceType.Name}{(receipt.DeviceBrand != null ? $" - {receipt.DeviceBrand.Name}" : "")}</span>
                            </div>
                        </div>
                        <div class=""problem-box"">
                            <div class=""problem-label"">📝 شرح مشکل دستگاه</div>
                            <div class=""problem-text"">{receipt.ProblemDescription}</div>
                        </div>
                    </div>
                </div>
            </div>
            
            {itemsHtml}
            
            <div class=""summary-box"">
                <div class=""summary-row"">
                    <span class=""summary-label"">جمع کل اقلام:</span>
                    <span class=""summary-value"">{total.ToString("N0")} ریال</span>
                </div>
                {(discount > 0 ? $@"
                <div class=""summary-row"">
                    <span class=""summary-label"">تخفیف:</span>
                    <span class=""summary-value discount-value"">{discount.ToString("N0")} ریال</span>
                </div>
                " : "")}
                <div class=""summary-row total-row"">
                    <span class=""summary-label"">مبلغ قابل پرداخت:</span>
                    <span class=""summary-value"">{payable.ToString("N0")} ریال</span>
                </div>
                <div class=""summary-row"">
                    <span class=""summary-label"">پرداختی:</span>
                    <span class=""summary-value paid-value"">{paid.ToString("N0")} ریال</span>
                </div>
                <div class=""summary-row"">
                    <span class=""summary-label"">{(remaining >= 0 ? "باقی‌مانده" : "طلب مشتری")}:</span>
                    <span class=""summary-value {(remaining > 0 ? "remaining-debtor" : remaining < 0 ? "remaining-creditor" : "")}"">{Math.Abs(remaining).ToString("N0")} ریال</span>
                </div>
            </div>
        </div>
        
        <div class=""invoice-footer"">
            <div class=""footer-contact"">
                {(!string.IsNullOrEmpty(website) ? $"<span class=\"contact-item\">🌐 {website}</span>" : "")}
                {(!string.IsNullOrEmpty(phone) ? $"<span class=\"contact-item\">📞 {phone}</span>" : "")}
                {(!string.IsNullOrEmpty(address) ? $"<span class=\"contact-item\">📍 {address}</span>" : "")}
            </div>
            <div class=""copyright"">
                این فاکتور الکترونیکی معتبر می‌باشد
            </div>
        </div>
    </div>
</body>
</html>");

        return htmlWriter.ToString();
    }

    private string GenerateBulkInvoiceHtml(List<CustomerReceipt> receipts, BulkPrintInvoiceRequest settings)
    {
        var htmlWriter = new StringWriter();

        var workshopName = !string.IsNullOrEmpty(settings.WorkshopName) ? settings.WorkshopName : "";
        var headerTitle = !string.IsNullOrEmpty(settings.HeaderTitle) ? settings.HeaderTitle : "گزارش فاکتورها";
        var address = !string.IsNullOrEmpty(settings.Address) ? settings.Address : "";
        var phone = !string.IsNullOrEmpty(settings.Phone) ? settings.Phone : "";
        var website = !string.IsNullOrEmpty(settings.Website) ? settings.Website : "";
        var printDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm");

        var logoHtml = !string.IsNullOrEmpty(settings.LogoUrl)
            ? $"<img src=\"{settings.LogoUrl}\" class=\"logo-img\" alt=\"logo\">"
            : "";

        var firstReceipt = receipts.First();

        long grandTotal = 0, grandDiscount = 0, grandPayable = 0, grandPaid = 0, grandRemaining = 0;

        var receiptsHtmlWriter = new StringWriter();
        foreach (var receipt in receipts)
        {
            var billing = receipt.Billing;
            var total = billing?.BillTotal ?? 0;
            var discount = billing?.Discount ?? 0;
            var payable = total - discount;
            var paid = (billing?.Prepaid ?? 0) + (billing?.Paid ?? 0);
            var remaining = payable - paid;
            var invoiceNumber = receipt.Id.ToString("D8");

            grandTotal += total;
            grandDiscount += discount;
            grandPayable += payable;
            grandPaid += paid;
            grandRemaining += remaining;

            var statusClass = receipt.Status switch
            {
                "pending" => "badge-warning",
                "repaired" => "badge-success",
                "delivered" => "badge-info",
                "not_repairable" => "badge-danger",
                _ => "badge-secondary"
            };

            var statusText = receipt.Status switch
            {
                "pending" => "در انتظار",
                "repaired" => "تعمیر شده",
                "delivered" => "تحویل شده",
                "not_repairable" => "غیر قابل تعمیر",
                _ => receipt.Status
            };

            var itemsHtmlWriter = new StringWriter();
            if (billing != null && billing.Items.Any())
            {
                int itemIndex = 1;
                foreach (var item in billing.Items)
                {
                    itemsHtmlWriter.WriteLine($@"
                        <tr>
                            <td class=""col-no"">{itemIndex++}</td>
                            <td class=""col-desc"">{item.Title}</td>
                            <td class=""col-price"">{item.UnitPrice.ToString("N0")} ریال</td>
                        </tr>");
                }
            }

            var itemsHtml = (billing != null && billing.Items.Any()) ? $@"
                <table class=""items-table"">
                    <thead>
                        <tr>
                            <th class=""col-no"">#</th>
                            <th class=""col-desc"">شرح</th>
                            <th class=""col-price"">مبلغ (ریال)</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemsHtmlWriter.ToString()}
                    </tbody>
                </table>
                <div class=""summary-mini"">
                    <span>جمع: {total.ToString("N0")} ریال</span>
                    {(discount > 0 ? $"<span>تخفیف: {discount.ToString("N0")} ریال</span>" : "")}
                    <span>قابل پرداخت: {payable.ToString("N0")} ریال</span>
                    <span>پرداختی: {paid.ToString("N0")} ریال</span>
                    <span class=""{(remaining > 0 ? "remaining-debtor" : remaining < 0 ? "remaining-creditor" : "")}"">مانده: {Math.Abs(remaining).ToString("N0")} ریال</span>
                </div>" : "<div class=\"empty-items-small\">بدون آیتم</div>";

            receiptsHtmlWriter.WriteLine($@"
            <div class=""receipt-card"">
                <div class=""receipt-card-header"">
                    <div class=""receipt-title"">
                        <span>📄</span>
                        <span>رسید #{receipt.Id}</span>
                        <span class=""badge {statusClass}"">{statusText}</span>
                    </div>
                    <div class=""receipt-date"">{receipt.RegisteredAt:yyyy/MM/dd}</div>
                </div>
                <div class=""receipt-card-body"">
                    <div class=""device-info"">🖥️ {receipt.DeviceType.Name}{(receipt.DeviceBrand != null ? $" - {receipt.DeviceBrand.Name}" : "")}</div>
                    {(settings.ShowInvoiceNumber ? $"<div class=\"invoice-num\">شماره فاکتور: {invoiceNumber}</div>" : "")}
                    {itemsHtml}
                </div>
            </div>");
        }

        var grandRemainingClass = grandRemaining > 0 ? "remaining-debtor" : grandRemaining < 0 ? "remaining-creditor" : "";

        htmlWriter.WriteLine($@"
<!DOCTYPE html>
<html lang=""fa"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <title>گزارش فاکتورها</title>
    <style>
        @font-face {{ font-family: 'Vazirmatn'; src: url('/fonts/vazirmatn/Vazirmatn-Regular.woff2') format('woff2'); }}
        @font-face {{ font-family: 'Vazirmatn'; font-weight: 700; src: url('/fonts/vazirmatn/Vazirmatn-Bold.woff2') format('woff2'); }}
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Vazirmatn', sans-serif; background: #eef2f7; padding: 25px; direction: rtl; }}
        .bulk-invoice {{ max-width: 800px; margin: 0 auto; background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 5px 15px rgba(0,0,0,0.1); border: 1px solid #e2e8f0; }}
        .bulk-header {{ background: #fff; padding: 18px 25px; text-align: center; border-bottom: 2px solid #e2e8f0; }}
        .logo-img {{ max-height: 50px; margin-bottom: 8px; }}
        .workshop-name {{ font-size: 0.8rem; color: #7f8c8d; }}
        .bulk-header h1 {{ font-size: 1.4rem; margin: 5px 0; color: #1a2c3e; }}
        .customer-summary {{ background: #f8fafc; padding: 12px 25px; display: flex; justify-content: space-between; flex-wrap: wrap; gap: 10px; border-bottom: 1px solid #e2e8f0; font-size: 0.8rem; }}
        .bulk-body {{ padding: 20px 25px; }}
        .receipt-card {{ background: #fff; border: 1px solid #14345f; border-radius: 10px; margin-bottom: 20px; overflow: hidden; }}
        .receipt-card-header {{ background: #fafcfc; padding: 12px 15px; display: flex; justify-content: space-between; flex-wrap: wrap; align-items: center; border-bottom: 1px solid #e2e8f0; font-size: 0.8rem; }}
        .receipt-title {{ display: flex; align-items: center; gap: 10px; font-weight: 600; }}
        .receipt-date {{ color: #7f8c8d; font-size: 0.7rem; }}
        .receipt-card-body {{ padding: 15px; }}
        .device-info {{ margin-bottom: 10px; color: #475569; font-size: 0.8rem; }}
        .invoice-num {{ font-size: 0.7rem; color: #2563eb; margin-bottom: 10px; }}
        .items-table {{ width: 100%; border-collapse: collapse; font-size: 0.7rem; margin: 10px 0; }}
        .items-table th {{ background: #f1f5f9; padding: 8px; text-align: center; border: 1px solid #cbd5e1; }}
        .items-table td {{ padding: 8px; border: 1px solid #e2e8f0; }}
        .col-no {{ width: 40px; text-align: center; }}
        .col-desc {{ text-align: right; }}
        .col-price {{ text-align: left; width: 110px; }}
        .summary-mini {{ background: #f8fafc; padding: 10px 12px; border-radius: 8px; margin-top: 12px; display: flex; justify-content: space-between; flex-wrap: wrap; gap: 10px; font-size: 0.7rem; border: 1px solid #e2e8f0; }}
        .remaining-debtor {{ color: #dc2626; font-weight: 600; }}
        .remaining-creditor {{ color: #059669; font-weight: 600; }}
        .empty-items-small {{ text-align: center; padding: 15px; color: #94a3b8; font-size: 0.7rem; }}
        .grand-summary {{ background: linear-gradient(135deg, #eef2ff, #e0e7ff); border-radius: 12px; padding: 16px 20px; margin-top: 24px; border: 2px solid #c7d2fe; }}
        .grand-title {{ font-size: 0.85rem; font-weight: 700; color: #1e3c72; margin-bottom: 12px; text-align: center; }}
        .grand-row {{ display: flex; justify-content: space-between; align-items: center; padding: 8px 0; border-bottom: 1px dashed #c7d2fe; font-size: 0.75rem; }}
        .grand-row:last-child {{ border-bottom: none; }}
        .grand-label {{ color: #475569; font-weight: 500; }}
        .grand-value {{ font-weight: 700; text-align: left; }}
        .grand-total {{ font-size: 0.85rem; font-weight: 800; color: #1e3c72; }}
        .bulk-footer {{ background: #f8fafc; padding: 12px 25px; text-align: center; border-top: 2px solid #e2e8f0; font-size: 0.65rem; color: #7f8c8d; }}
        .badge {{ display: inline-block; padding: 2px 10px; border-radius: 20px; font-size: 0.65rem; font-weight: 500; border: 1px solid; }}
        .badge-warning {{ background: #fef3c7; color: #d97706; border-color: #fde68a; }}
        .badge-success {{ background: #d1fae5; color: #059669; border-color: #a7f3d0; }}
        .badge-info {{ background: #dbeafe; color: #2563eb; border-color: #bfdbfe; }}
        .badge-danger {{ background: #fee2e2; color: #dc2626; border-color: #fecaca; }}
        .discount-value {{ color: #dc2626; }}
        .paid-value {{ color: #059669; }}
        @media print {{ body {{ background: white; padding: 0; }} }}
        @media (max-width: 600px) {{ body {{ padding: 15px; }} .bulk-body, .bulk-header, .customer-summary {{ padding: 15px; }} .summary-mini, .grand-row {{ flex-direction: column; text-align: center; }} }}
    </style>
</head>
<body>
    <div class=""bulk-invoice"">
        <div class=""bulk-header"">
            {logoHtml}
            <h1>{headerTitle}</h1>
            {(!string.IsNullOrEmpty(workshopName) ? $"<div class=\"workshop-name\">{workshopName}</div>" : "")}
            {(settings.ShowPrintDate ? $"<div style=\"font-size:0.7rem; color:#7f8c8d; margin-top:5px;\">تاریخ چاپ: {printDate}</div>" : "")}
        </div>
        
        <div class=""customer-summary"">
            <div><strong>👤 مشتری:</strong> {firstReceipt.Customer.FirstName} {firstReceipt.Customer.LastName}</div>
            <div><strong>📱 موبایل:</strong> {firstReceipt.Customer.Mobile}</div>
            <div><strong>📅 بازه:</strong> {settings.DateFrom} تا {settings.DateTo}</div>
            <div><strong>📊 تعداد:</strong> {receipts.Count}</div>
        </div>
        
        <div class=""bulk-body"">
            {receiptsHtmlWriter.ToString()}
            
            <div class=""grand-summary"">
                <div class=""grand-title"">جمع کل صورت حساب‌ها</div>
                <div class=""grand-row"">
                    <span class=""grand-label"">مجموع کل:</span>
                    <span class=""grand-value grand-total"">{grandTotal.ToString("N0")} ریال</span>
                </div>
                {(grandDiscount > 0 ? $@"
                <div class=""grand-row"">
                    <span class=""grand-label"">مجموع تخفیف:</span>
                    <span class=""grand-value discount-value"">{grandDiscount.ToString("N0")} ریال</span>
                </div>
                " : "")}
                <div class=""grand-row"">
                    <span class=""grand-label"">مجموع قابل پرداخت:</span>
                    <span class=""grand-value"">{grandPayable.ToString("N0")} ریال</span>
                </div>
                <div class=""grand-row"">
                    <span class=""grand-label"">مجموع پرداخت شده:</span>
                    <span class=""grand-value paid-value"">{grandPaid.ToString("N0")} ریال</span>
                </div>
                <div class=""grand-row"">
                    <span class=""grand-label"">{(grandRemaining >= 0 ? "مجموع مانده" : "مجموع طلب مشتری")}:</span>
                    <span class=""grand-value {grandRemainingClass}"">{Math.Abs(grandRemaining).ToString("N0")} ریال</span>
                </div>
            </div>
        </div>
        
        <div class=""bulk-footer"">
            <div>{website}  |  {phone}  |  {address}</div>
        </div>
    </div>
</body>
</html>");

        return htmlWriter.ToString();
    }
}