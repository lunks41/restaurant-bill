using System.ComponentModel.DataAnnotations;

namespace RestaurantBilling.Models.Masters;

public class CategoryInputModel
{
    public int? CategoryId { get; set; }

    [Required]
    [StringLength(120)]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0, 999)]
    public int SortOrder { get; set; }
}

