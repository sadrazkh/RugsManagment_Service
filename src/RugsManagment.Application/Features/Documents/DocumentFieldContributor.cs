using RugsManagment.Application.Fields;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;
namespace RugsManagment.Application.Features.Documents;
public sealed class DocumentFieldContributor(IRepository<CustomFieldDefinition> customFields) : IFieldCatalogContributor
{
    public int Order => 500;
    public async Task<IReadOnlyList<FieldDefDto>> FieldsAsync(FieldCatalogRequest request, CancellationToken cancellationToken)
    {
        if (request.Purpose != FieldPurpose.Document) return [];
        var result = DocumentFields.Catalogue().ToList();
        var definitions = await customFields.ListAsync(x => x.TenantId == request.TenantId && (x.Key == "bg_color" || x.Key == "border_color"),cancellationToken);
        // قالب آماده حتی پیش از اجرای بسته کار کند؛ تعریف صریحِ پنهان/غیرفعال هرگز با این پیش‌فرض دور زده نمی‌شود.
        if (!definitions.Any(x => x.Key == "bg_color")) result.Add(DocumentFields.CustomText("bg_color","رنگ متن"));
        if (!definitions.Any(x => x.Key == "border_color")) result.Add(DocumentFields.CustomText("border_color","رنگ حاشیه"));
        return result;
    }
}
