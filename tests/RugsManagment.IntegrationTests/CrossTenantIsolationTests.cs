using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.DTOs.CustomFields;
using RugsManagment.Application.DTOs.Labels;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence.Repositories;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// رگرسیون جداسازی کارگاه‌ها: کاربرِ کارگاه A شناسه‌ای از کارگاه B می‌فرستد.
///
/// قاعده: شناسهٔ کارگاه دیگر درست مثل شناسهٔ ناموجود رفتار می‌کند — ۴۰۴ (KeyNotFoundException)
/// یا ۴۰۰ (InvalidOperationException با پیام فارسی)، هرگز ۵۰۰ (خطای کلید خارجی) و هرگز نوشتنِ
/// بی‌صدا روی دادهٔ کارگاه دیگر. هر تست بعد از رد شدن، دیتابیس را با DbContext مستقل می‌خواند
/// تا مطمئن شود واقعاً هیچ چیز عوض نشده است.
///
/// سرویس‌ها با DI واقعی ساخته می‌شوند (<see cref="TwoTenantFixture"/>) و هر scope یک «درخواست» است.
/// </summary>
public sealed class CrossTenantIsolationTests(TwoTenantFixture fixture) : IClassFixture<TwoTenantFixture>
{
    private TenantSeed A => fixture.A;
    private TenantSeed B => fixture.B;

    // ── ۱. اختصاص فرش به گروه ───────────────────────────────────────────

    [PostgresFact]
    public async Task AssignRugs_ToOtherTenantsBatch_IsNotFound_AndChangesNothing()
    {
        var rug = await fixture.CreateRugAsync(A);

        await using (var scope = fixture.As(A))
        {
            var service = scope.ServiceProvider.GetRequiredService<IRugBatchService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.AssignRugsAsync(A.TenantId, B.BatchId, new([rug.Id])));
        }

        await using (var scope = fixture.As(A))
        {
            // گروه ناموجود هم همان پاسخ را می‌گیرد — وجود گروه B لو نمی‌رود
            var service = scope.ServiceProvider.GetRequiredService<IRugBatchService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.AssignRugsAsync(A.TenantId, Guid.NewGuid(), new([rug.Id])));
        }

        await using (var db = fixture.CreateContext())
        {
            Assert.Null((await db.Rugs.SingleAsync(r => r.Id == rug.Id)).BatchId);
            Assert.False(await db.Rugs.AnyAsync(r => r.BatchId == B.BatchId && r.TenantId == A.TenantId));
        }

        // کنترل مثبت: گروه خودِ کارگاه کار می‌کند
        await using (var scope = fixture.As(A))
        {
            await scope.ServiceProvider.GetRequiredService<IRugBatchService>()
                .AssignRugsAsync(A.TenantId, A.BatchId, new([rug.Id]));
        }

        await using (var db = fixture.CreateContext())
            Assert.Equal(A.BatchId, (await db.Rugs.SingleAsync(r => r.Id == rug.Id)).BatchId);
    }

    [PostgresFact]
    public async Task AssignRugs_WithOtherTenantsRug_IsNotFound_AndRollsBackTheWholeRequest()
    {
        var ownRug = await fixture.CreateRugAsync(A);
        var foreignRug = await fixture.CreateRugAsync(B);

        await using (var scope = fixture.As(A))
        {
            var service = scope.ServiceProvider.GetRequiredService<IRugBatchService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.AssignRugsAsync(A.TenantId, A.BatchId, new([ownRug.Id, foreignRug.Id])));
        }

        await using var db = fixture.CreateContext();
        Assert.Null((await db.Rugs.SingleAsync(r => r.Id == ownRug.Id)).BatchId);
        Assert.Null((await db.Rugs.SingleAsync(r => r.Id == foreignRug.Id)).BatchId);
    }

    // ── ۲. ویرایش گروهی با BatchId بیگانه ───────────────────────────────

    [PostgresFact]
    public async Task BulkUpdateFields_WithOtherTenantsOrUnknownBatch_IsBadRequest_NotForeignKeyFailure()
    {
        var rug = await fixture.CreateRugAsync(A, A.BatchId);

        foreach (var batchId in new[] { B.BatchId, Guid.NewGuid() })
        {
            await using var scope = fixture.As(A);
            var service = scope.ServiceProvider.GetRequiredService<IRugManagementService>();

            var error = await Record.ExceptionAsync(() => service.BulkUpdateFieldsAsync(
                A.TenantId, BulkFields([rug.Id], origin: "Tabriz", batchId: batchId)));

            // دقیقاً InvalidOperationException (۴۰۰ در ApiExceptionFilter). قبلاً گروه ناموجود به
            // DbUpdateException (نقض کلید خارجی، ۵۰۰) می‌رسید و گروه کارگاه دیگر بی‌صدا ذخیره می‌شد.
            Assert.NotNull(error);
            Assert.IsType<InvalidOperationException>(error);
        }

        // کل درخواست رد شده — حتی فیلد Origin هم اعمال نشده
        await using (var db = fixture.CreateContext())
        {
            var stored = await db.Rugs.SingleAsync(r => r.Id == rug.Id);
            Assert.Equal(A.BatchId, stored.BatchId);
            Assert.Null(stored.Origin);
        }

        // کنترل مثبت: Guid.Empty یعنی «خروج از گروه» و همچنان کار می‌کند
        await using (var scope = fixture.As(A))
        {
            var result = await scope.ServiceProvider.GetRequiredService<IRugManagementService>()
                .BulkUpdateFieldsAsync(A.TenantId, BulkFields([rug.Id], origin: "Tabriz", batchId: Guid.Empty));
            Assert.Equal(1, result.SuccessCount);
        }

        await using (var db = fixture.CreateContext())
        {
            var stored = await db.Rugs.SingleAsync(r => r.Id == rug.Id);
            Assert.Null(stored.BatchId);
            Assert.Equal("Tabriz", stored.Origin);
        }
    }

    // ── ۳. نوع مرحله: سیستمی آری، اختصاصیِ کارگاه دیگر نه ───────────────

    [PostgresFact]
    public async Task ProcessStepTypeLookup_RefusesOtherTenantsPrivateType_ButReturnsSystemAndOwnTypes()
    {
        await using (var scope = fixture.As(A))
        {
            var lookup = scope.ServiceProvider.GetRequiredService<IProcessStepTypeLookup>();
            Assert.IsType<ProcessStepTypeLookup>(lookup); // همان پیاده‌سازی scoped که DI واقعی ثبت می‌کند

            await Assert.ThrowsAsync<KeyNotFoundException>(() => lookup.GetRequiredAsync(B.PrivateStepTypeId));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => lookup.GetRequiredAsync(Guid.NewGuid()));
            Assert.Equal(fixture.SystemStepTypeId, (await lookup.GetRequiredAsync(fixture.SystemStepTypeId)).Id);
            Assert.Equal(A.PrivateStepTypeId, (await lookup.GetRequiredAsync(A.PrivateStepTypeId)).Id);
        }

        // بدون کارگاه (ادمین سیستم) فقط نوع‌های سیستمی دیده می‌شوند
        await using (var scope = fixture.AsSystemAdmin())
        {
            var lookup = scope.ServiceProvider.GetRequiredService<IProcessStepTypeLookup>();
            Assert.Equal(fixture.SystemStepTypeId, (await lookup.GetRequiredAsync(fixture.SystemStepTypeId)).Id);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => lookup.GetRequiredAsync(A.PrivateStepTypeId));
        }
    }

    [PostgresFact]
    public async Task RugWorkflow_CannotUseOtherTenantsPrivateStepType()
    {
        var rug = await fixture.CreateRugAsync(A);
        var rugCountBefore = await CountRugsAsync(A.TenantId);

        await using (var scope = fixture.As(A))
        {
            var service = scope.ServiceProvider.GetRequiredService<IRugManagementService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(
                A.TenantId, TwoTenantFixture.NewRugRequest([new(B.PrivateStepTypeId, false, null)])));
        }

        await using (var scope = fixture.As(A))
        {
            var service = scope.ServiceProvider.GetRequiredService<IRugManagementService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateWorkflowAsync(
                A.TenantId, rug.Id, new UpdateRugWorkflowRequest([new(B.PrivateStepTypeId, false, null)])));
        }

        Assert.Equal(rugCountBefore, await CountRugsAsync(A.TenantId));
        await using var db = fixture.CreateContext();
        Assert.False(await db.RugWorkflowSteps.AnyAsync(s => s.ProcessStepTypeId == B.PrivateStepTypeId));
        Assert.Equal(new[] { fixture.SystemStepTypeId },
            await db.RugWorkflowSteps.Where(s => s.RugId == rug.Id).Select(s => s.ProcessStepTypeId).ToListAsync());
    }

    // ── ۴. فیلد سفارشی و قالب برچسب ───────────────────────────────────

    [PostgresFact]
    public async Task CustomFieldService_OtherTenantsField_IsNotFound_AndUntouched()
    {
        await using (var scope = fixture.As(A))
        {
            var service = scope.ServiceProvider.GetRequiredService<ICustomFieldService>();

            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsync(
                A.TenantId, B.CustomFieldId,
                new UpdateCustomFieldRequest("hijacked", CustomFieldType.Number, null, true, 99, false)));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(A.TenantId, B.CustomFieldId));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(A.TenantId, Guid.NewGuid()));

            var visible = await service.ListAsync(A.TenantId, onlyActive: false);
            Assert.Equal(new[] { A.CustomFieldId }, visible.Select(f => f.Id));
        }

        await using var db = fixture.CreateContext();
        var stored = await db.CustomFieldDefinitions.SingleAsync(f => f.Id == B.CustomFieldId);
        Assert.Equal("Border B", stored.Label);
        Assert.Equal(CustomFieldType.Text, stored.FieldType);
        Assert.True(stored.IsActive);
        Assert.False(stored.IsRequired);
    }

    [PostgresFact]
    public async Task LabelTemplateService_OtherTenantsTemplate_IsNotFound_AndUntouched()
    {
        await using (var scope = fixture.As(A))
        {
            var service = scope.ServiceProvider.GetRequiredService<ILabelTemplateService>();

            Assert.Null(await service.GetAsync(A.TenantId, B.LabelId));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsync(
                A.TenantId, B.LabelId, new SaveLabelTemplateRequest("hijacked", 40, 40, LabelMode.Html, null, "<b>x</b>")));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(A.TenantId, B.LabelId));

            var visible = await service.ListAsync(A.TenantId);
            Assert.Equal(new[] { A.LabelId }, visible.Select(l => l.Id));
        }

        await using var db = fixture.CreateContext();
        var stored = await db.LabelTemplates.SingleAsync(l => l.Id == B.LabelId);
        Assert.Equal("Label B", stored.Name);
        Assert.Equal(LabelMode.Visual, stored.Mode);
        Assert.Null(stored.HtmlContent);
    }

    // ── ۵. طرف خدماتِ کارگاه دیگر روی مرحله ───────────────────────────

    [PostgresFact]
    public async Task OtherTenantsServiceProvider_IsRefused_OnEveryStepWritePath()
    {
        var rug = await fixture.CreateRugAsync(A);
        var stepId = rug.WorkflowSteps.Single().Id;
        var rugCountBefore = await CountRugsAsync(A.TenantId);
        var foreignProvider = new AdvanceRugStepRequest(B.ProviderId, null, null, null, null, null, null);

        // هر مسیر در «درخواست» جدا، تا ردیابیِ شکست قبلی روی بعدی اثر نگذارد
        await AsA(s => s.AdvanceStepAsync(A.TenantId, rug.Id, stepId, foreignProvider));
        await AsA(s => s.UpdateStepPricingAsync(A.TenantId, rug.Id, stepId, foreignProvider));
        await AsA(s => s.BulkAdvanceAsync(A.TenantId, new BulkAdvanceRequest([rug.Id], foreignProvider)));
        await AsA(s => s.CreateAsync(A.TenantId,
            TwoTenantFixture.NewRugRequest([new(fixture.SystemStepTypeId, false, B.ProviderId)])));
        await AsA(s => s.UpdateWorkflowAsync(A.TenantId, rug.Id,
            new UpdateRugWorkflowRequest([new(fixture.SystemStepTypeId, false, B.ProviderId)])));
        await AsA(s => s.BulkUpdateWorkflowAsync(A.TenantId,
            new BulkUpdateWorkflowRequest([rug.Id], [new(fixture.SystemStepTypeId, false, B.ProviderId)])));

        Assert.Equal(rugCountBefore, await CountRugsAsync(A.TenantId));
        await using (var db = fixture.CreateContext())
        {
            // حساب طرف B هیچ کاری از کارگاه A نگرفته و مرحله دست‌نخورده است
            Assert.False(await db.RugWorkflowSteps.AnyAsync(s => s.ServiceProviderId == B.ProviderId));
            var step = await db.RugWorkflowSteps.SingleAsync(s => s.Id == stepId);
            Assert.Equal(WorkflowStepStatus.InProgress, step.Status);
            Assert.Null(step.ServiceProviderId);
            Assert.Null(step.CompletedAt);
            Assert.Null(step.AppliedUnitRate);
        }

        // کنترل مثبت: طرف خودِ کارگاه پذیرفته می‌شود و نرخ توافقی‌اش (نه پیش‌فرض) اعمال می‌شود
        var ownProvider = foreignProvider with { ServiceProviderId = A.ProviderId };
        await using (var scope = fixture.As(A))
        {
            var advanced = await scope.ServiceProvider.GetRequiredService<IRugManagementService>()
                .AdvanceStepAsync(A.TenantId, rug.Id, stepId, ownProvider);
            var step = advanced.WorkflowSteps.Single();
            Assert.Equal(WorkflowStepStatus.Completed, step.Status);
            Assert.Equal(A.ProviderId, step.ServiceProviderId);
            Assert.Equal(TwoTenantFixture.ProviderAgreedRate * 2m * 3m, step.CalculatedCost);
        }

        async Task AsA(Func<IRugManagementService, Task> action)
        {
            await using var scope = fixture.As(A);
            var service = scope.ServiceProvider.GetRequiredService<IRugManagementService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => action(service));
        }
    }

    // ── ۶. قالب گردش کار: نوع مرحله و طرف خدماتِ کارگاه دیگر ───────────

    [PostgresFact]
    public async Task WorkflowTemplate_CannotReferenceOtherTenantsStepTypeOrProvider()
    {
        var foreignType = TemplateWith(B.PrivateStepTypeId, providerId: null);
        var foreignProvider = TemplateWith(fixture.SystemStepTypeId, providerId: B.ProviderId);
        var templatesBefore = await CountTemplatesAsync(A.TenantId);

        foreach (var steps in new[] { foreignType, foreignProvider })
        {
            await using (var scope = fixture.As(A))
            {
                var service = scope.ServiceProvider.GetRequiredService<IWorkflowManagementService>();
                await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateTemplateAsync(
                    A.TenantId, new CreateWorkflowTemplateRequest("hijack", null, false, steps)));
            }

            await using (var scope = fixture.As(A))
            {
                var service = scope.ServiceProvider.GetRequiredService<IWorkflowManagementService>();
                await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateTemplateAsync(
                    A.TenantId, A.WorkflowTemplateId,
                    new UpdateWorkflowTemplateRequest("hijack", null, false, true, steps)));
            }
        }

        // ناموجود هم همان پاسخ را می‌گیرد — وجود شناسهٔ B لو نمی‌رود
        await using (var scope = fixture.As(A))
        {
            var service = scope.ServiceProvider.GetRequiredService<IWorkflowManagementService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateTemplateAsync(
                A.TenantId, new CreateWorkflowTemplateRequest("ghost", null, false, TemplateWith(Guid.NewGuid(), null))));
        }

        Assert.Equal(templatesBefore, await CountTemplatesAsync(A.TenantId));
        await using (var db = fixture.CreateContext())
        {
            Assert.False(await db.WorkflowTemplateSteps.AnyAsync(s =>
                s.ProcessStepTypeId == B.PrivateStepTypeId || s.DefaultServiceProviderId == B.ProviderId));
            var stored = await db.WorkflowTemplates.SingleAsync(t => t.Id == A.WorkflowTemplateId);
            Assert.Equal("Template A", stored.Name);
        }

        // کنترل مثبت: نوع سیستمی + نوع اختصاصیِ خود + طرف خودِ کارگاه
        await using (var scope = fixture.As(A))
        {
            var created = await scope.ServiceProvider.GetRequiredService<IWorkflowManagementService>()
                .CreateTemplateAsync(A.TenantId, new CreateWorkflowTemplateRequest("own", null, false,
                [
                    new(fixture.SystemStepTypeId, 0, false, A.ProviderId, null, null),
                    new(A.PrivateStepTypeId, 1, true, null, null, null)
                ]));
            Assert.Equal(2, created.Steps.Count);
        }
    }

    private static IReadOnlyList<CreateWorkflowTemplateStepRequest> TemplateWith(Guid stepTypeId, Guid? providerId)
        => [new(stepTypeId, 0, false, providerId, null, null)];

    private async Task<int> CountTemplatesAsync(Guid tenantId)
    {
        await using var db = fixture.CreateContext();
        return await db.WorkflowTemplates.CountAsync(t => t.TenantId == tenantId);
    }

    // ── تنظیمات کارگاه: قالب پیش‌فرض ───────────────────────────────────

    [PostgresFact]
    public async Task TenantSettings_DefaultTemplateOfOtherTenant_IsBadRequest_AndNotStored()
    {
        await using (var scope = fixture.As(A))
        {
            var service = scope.ServiceProvider.GetRequiredService<ITenantSettingsService>();
            var error = await Record.ExceptionAsync(() => service.UpdateAsync(A.TenantId,
                new UpdateTenantSettingsRequest("IT tenant A", null, null, CurrencyUnit.Toman, B.WorkflowTemplateId)));
            Assert.IsType<InvalidOperationException>(error);
        }

        await using (var db = fixture.CreateContext())
            Assert.Null((await db.Tenants.SingleAsync(t => t.Id == A.TenantId)).DefaultWorkflowTemplateId);

        await using (var scope = fixture.As(A))
        {
            var updated = await scope.ServiceProvider.GetRequiredService<ITenantSettingsService>().UpdateAsync(A.TenantId,
                new UpdateTenantSettingsRequest("IT tenant A", null, null, CurrencyUnit.Toman, A.WorkflowTemplateId));
            Assert.Equal(A.WorkflowTemplateId, updated.DefaultWorkflowTemplateId);
        }
    }

    private async Task<int> CountRugsAsync(Guid tenantId)
    {
        await using var db = fixture.CreateContext();
        return await db.Rugs.IgnoreQueryFilters().CountAsync(r => r.TenantId == tenantId);
    }

    private static BulkUpdateFieldsRequest BulkFields(IReadOnlyList<Guid> rugIds, string? origin, Guid? batchId)
        => new(rugIds, origin, null, null, null, null, null, batchId);
}
