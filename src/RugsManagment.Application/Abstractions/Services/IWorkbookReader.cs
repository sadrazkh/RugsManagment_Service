using RugsManagment.Application.Features.Import;
namespace RugsManagment.Application.Abstractions.Services;
public interface IWorkbookReader
{
    Task<IReadOnlyList<SheetGrid>> ReadAsync(Stream input, CancellationToken ct = default);
}
