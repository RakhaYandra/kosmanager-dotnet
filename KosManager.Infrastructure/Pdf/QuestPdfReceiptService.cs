using KosManager.Application.Billing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KosManager.Infrastructure.Pdf;

public class QuestPdfReceiptService : IReceiptService
{
    static QuestPdfReceiptService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(ReceiptData d)
    {
        var rp = d.Amount.ToString("N0", new System.Globalization.CultureInfo("id-ID"));
        var lunas = d.Status == "paid";
        return Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A5);
                p.Margin(24);
                p.Header().Column(col =>
                {
                    col.Item().Text(d.KosName).FontSize(20).Bold();
                    col.Item().Text("Tanda terima pembayaran kos").FontSize(11).FontColor(Colors.Grey.Darken1);
                });
                p.Content().PaddingVertical(12).Column(col =>
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
                        void Row(string k, string v)
                        {
                            t.Cell().Text(k).FontSize(11).FontColor(Colors.Grey.Darken1);
                            t.Cell().AlignRight().Text(v).FontSize(11).SemiBold();
                        }
                        Row("Penghuni", d.Tenant);
                        Row("Kamar", d.Room);
                        Row("Periode", d.Period);
                        Row("Nominal", $"Rp{rp}");
                        Row("Jatuh tempo", d.DueDate.ToString("dd MMM yyyy"));
                        Row("Status", lunas ? "LUNAS" : "BELUM LUNAS");
                        Row("Dicetak", d.Printed.ToString("dd MMM yyyy"));
                    });
                    col.Item().PaddingTop(16).AlignCenter()
                        .Text(lunas ? "LUNAS" : "BELUM LUNAS")
                        .FontSize(18).Bold()
                        .FontColor(lunas ? Colors.Green.Darken1 : Colors.Red.Darken1);
                });
                p.Footer().AlignCenter().Text("Dokumen otomatis KosManager — data contoh").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }
}
