using Entities.Configuration;
using Entities.Masters;

namespace IServices;

public interface IMasterService
{
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Item>> GetItemsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TaxConfiguration>> GetTaxesAsync(CancellationToken cancellationToken);
}
