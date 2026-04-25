using System.ComponentModel.DataAnnotations;
using Entities.Enums;
using Microsoft.AspNetCore.Http;

namespace RestaurantBilling.Models.Masters;

public class ItemInputModel
{
    public int? ItemId { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Required]
    [StringLength(30)]
    public string ItemCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Range(0, 999999)]
    public decimal SalePrice { get; set; }

    [Range(0, 999999)]
    public decimal PurchasePrice { get; set; }

    [Range(0, 100)]
    public decimal GstPercent { get; set; }

    public bool IsTaxInclusive { get; set; }
    public TaxType TaxType { get; set; } = TaxType.GST;

    [StringLength(10)]
    public string SacCode { get; set; } = "996331";

    public bool IsStockTracked { get; set; }

    [Range(0, 999999)]
    public decimal ReorderLevel { get; set; }

    public string? ImagePath { get; set; }
    public IFormFile? ImageFile { get; set; }
}

