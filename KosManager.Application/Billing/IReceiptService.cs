namespace KosManager.Application.Billing;

public record ReceiptData(
    string KosName,
    string Tenant,
    string Room,
    string Period,
    decimal Amount,
    DateOnly DueDate,
    string Status,
    DateOnly Printed);

public interface IReceiptService
{
    byte[] Render(ReceiptData data);
}
