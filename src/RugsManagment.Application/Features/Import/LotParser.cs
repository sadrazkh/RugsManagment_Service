using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Import;
using RugsManagment.Domain.Common;
namespace RugsManagment.Application.Features.Import;
public static class LotParser
{
    public static int Quantity(SerialNumber? serial, string? quantity, string? material, List<ImportIssueDto> issues)
    {
        var number = ImportPlanner.Number(quantity);
        if (!string.IsNullOrWhiteSpace(quantity) && number is null)
        { issues.Add(ImportMessages.Issue("E_QTY_INVALID", "qty")); return 1; }
        if (number is null && serial?.IsRange == true && ImportPlanner.Number(material) is { } inferred)
        {
            number = inferred;
            issues.Add(ImportMessages.Issue("W_QTY_FROM_MATERIAL", "qty"));
        }
        number ??= serial?.IsRange == true ? serial.Count : 1;
        if (number != decimal.Truncate(number.Value) || number is < 1 or > 10000 || serial is not null && !serial.Covers((int)number.Value))
        {
            issues.Add(ImportMessages.Issue("E_QTY_INVALID", "qty"));
            return 1;
        }
        if (serial?.IsRange == true && number != serial.Count) issues.Add(ImportMessages.Issue("W_RANGE_QTY_DIFF", "qty"));
        return (int)number;
    }
}
