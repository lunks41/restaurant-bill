using Entities.Sales;

namespace IServices;

public interface IPrintService
{
    Task<byte[]> RenderBillThermalAsync(Bill bill, CancellationToken cancellationToken);
    Task<Stream> RenderBillPdfAsync(Bill bill, CancellationToken cancellationToken);
}

