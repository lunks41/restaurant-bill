using Data.Persistence;
using Entities.Configuration;
using Entities.Masters;
using IServices;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class MasterService(AppDbContext db) : IMasterService
{
    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken)
        => await db.Categories.OrderBy(x => x.SortOrder).ThenBy(x => x.CategoryName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Item>> GetItemsAsync(CancellationToken cancellationToken)
        => await db.Items.OrderBy(x => x.ItemName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaxConfiguration>> GetTaxesAsync(CancellationToken cancellationToken)
        => await db.TaxConfigurations.OrderByDescending(x => x.EffectiveFrom).ToListAsync(cancellationToken);
}
