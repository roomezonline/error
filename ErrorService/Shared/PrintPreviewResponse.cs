using System;
using System.Collections.Generic;
using System.Text;

namespace ErrorService.Shared
{
    public class PrintPreviewResponse
    {
        public string Html { get; set; } = "";
        public string Title { get; set; } = "";
        public int? ReceiptId { get; set; }
        public string CustomerName { get; set; } = "";
    }
}