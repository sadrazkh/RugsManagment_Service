using System.Text.RegularExpressions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Users;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>نقش‌ها، پیش‌فرض‌ها، ترجیحات کاربر و کارگاه، ترمزهای کاربران و قاعدهٔ پول در سرویس فرش.</summary>
public partial class PlatformServicesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // ═════════════════════════════════════════════════════════════════════
    // نقش‌ها و پیش‌فرض‌ها
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(UserRole.TenantAdmin, "تنظیم‌کننده (مدیر)")]
    [InlineData(UserRole.Merchant, "بازرگان")]
    [InlineData(UserRole.Operator, "کاربر / انباردار")]
    [InlineData(UserRole.SystemAdmin, "مدیر سامانه")]
    public void RoleLabels_ComeFromOnePlace(UserRole role, string expected)
    {
        Assert.Equal(expected, RoleLabels.For(role));
        Assert.Equal(expected, RoleLabels.For(role.ToString()));
    }

    [Fact]
    public void RoleLabels_TenantRoles_IncludeMerchant_ButNotSystemAdmin()
    {
        Assert.Equal(new[] { UserRole.Merchant, UserRole.Operator, UserRole.TenantAdmin }, RoleLabels.TenantRoles);
        Assert.False(RoleLabels.IsTenantRole(UserRole.SystemAdmin));
        Assert.Equal("نقش ناشناخته", RoleLabels.For("NotARole"));
        Assert.Equal("نقش ناشناخته", RoleLabels.For((string?)null));
    }

    [Theory]
    [InlineData(UserRole.Merchant, UiMode.Simple, TextScale.Large)]
    [InlineData(UserRole.Operator, UiMode.Simple, TextScale.Normal)]
    [InlineData(UserRole.TenantAdmin, UiMode.Full, TextScale.Normal)]
    [InlineData(UserRole.SystemAdmin, UiMode.Full, TextScale.Normal)]
    public void RoleDefaults_MatchSpecA3(UserRole role, UiMode mode, TextScale scale)
    {
        var user = new User { Role = role, GridEnter = GridEnterDirection.Right, TextScale = TextScale.XLarge };
        user.ApplyRoleDefaultPreferences();
        Assert.Equal((mode, scale), (user.UiMode, user.TextScale));
        Assert.Equal(GridEnterDirection.Right, user.GridEnter); // جهت Enter دست نمی‌خورد
    }

    [Fact]
    public void TenantPreferencesDefaults_MatchSpecB3()
    {
        var p = TenantPreferences.CreateDefault(TenantId);
        Assert.Equal(TenantId, p.TenantId);
        Assert.Equal("RUG", p.SkuPrefix);
        Assert.Equal(SerialMode.SuggestNext, p.SerialMode);
        Assert.Null(p.SerialStart);
        Assert.Equal(0, p.MoneyScale);
        Assert.Equal(12m, p.MaxPlausibleSideM);
        Assert.Equal(AllocationMethod.ByArea, p.DefaultMethod);
        Assert.Equal(AreaBasis.Effective, p.DefaultAreaBasis);
        Assert.Equal("rug.origin", p.DeclaredValueKeyField);
        Assert.Equal(0, p.DeclaredValuesStamp);
        Assert.Equal(10, p.BaleTargetTolerancePercent);
        Assert.True(p.RequireFinalSizeToFinalize);
        Assert.Equal(BaleTerm.Negleh, p.BaleTerm);
        Assert.Null(p.DefaultDeclarationTemplateId);
    }

    // ═════════════════════════════════════════════════════════════════════
    // برچسب رویدادهای تاریخچه
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void EveryAuditAction_HasAPersianLabel()
    {
        foreach (var action in Enum.GetValues<AuditAction>())
        {
            var label = ActivityLogService.ActionLabel(action);
            Assert.NotEqual("رویداد", label); // برچسب پیش‌فرضِ مقدار ناشناخته
            Assert.DoesNotMatch(LatinLetter(), label);
        }

        Assert.Equal(Enum.GetValues<AuditAction>().Length,
            Enum.GetValues<AuditAction>().Select(a => ActivityLogService.ActionLabel(a)).Distinct().Count());
    }

    [Fact]
    public void BaleLabels_FollowTheTenantsBaleTerm()
    {
        Assert.Equal("نگله‌بندی", ActivityLogService.ActionLabel(AuditAction.LinesChanged));
        Assert.Equal("ساخت عدل", ActivityLogService.ActionLabel(AuditAction.BaleCreated, BaleTerms.Adl));
        Assert.Equal("شماره‌گذاری دوبارهٔ عدل‌ها", ActivityLogService.ActionLabel(AuditAction.BalesRenumbered, BaleTerms.Adl));
    }

    [Fact]
    public void MoneyActions_CoverSalesPaymentsAndCosts()
    {
        Assert.Contains(AuditAction.SaleRecorded, ActivityLogService.MoneyActions);
        Assert.Contains(AuditAction.ProviderPaid, ActivityLogService.MoneyActions);
        Assert.Contains(AuditAction.CostEntryCreated, ActivityLogService.MoneyActions);
        Assert.DoesNotContain(AuditAction.StepAdvanced, ActivityLogService.MoneyActions);
    }

    // ═════════════════════════════════════════════════════════════════════
    // مدیریت کاربران
    // ═════════════════════════════════════════════════════════════════════

    private static (UserManagementService Service, FakeUserRepository Users, FakeAuditLog Audit, User Admin) Users(
        Guid? currentUserId = null)
    {
        var users = new FakeUserRepository();
        var admin = NewUser(UserRole.TenantAdmin);
        users.Users.Add(admin);
        var audit = new FakeAuditLog();
        var service = new UserManagementService(users, audit, new FakeUnitOfWork(),
            new FakeCurrentUser(currentUserId ?? admin.Id, TenantId));
        return (service, users, audit, admin);
    }

    private static User NewUser(UserRole role, bool active = true)
    {
        var user = new User
        {
            TenantId = TenantId,
            Email = $"{Guid.NewGuid():N}@t.test",
            FullName = role.ToString(),
            Role = role,
            IsActive = active,
            PasswordHash = "x"
        };
        user.ApplyRoleDefaultPreferences();
        return user;
    }

    [Fact]
    public async Task CreateMerchant_GetsSimpleModeAndLargeText()
    {
        var (service, users, audit, _) = Users();

        var dto = await service.CreateAsync(TenantId, new CreateUserRequest("پدر", "father@t.test", "Passw0rd-1", UserRole.Merchant));

        var stored = users.Users.Single(u => u.Id == dto.Id);
        Assert.Equal(UserRole.Merchant, stored.Role);
        Assert.Equal((UiMode.Simple, TextScale.Large), (stored.UiMode, stored.TextScale));
        Assert.Contains("بازرگان", audit.Records.Single().Summary);
    }

    [Fact]
    public async Task Create_RejectsSystemAdminRole_AsValidation()
    {
        var (service, _, _, _) = Users();
        var ex = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.CreateAsync(TenantId, new CreateUserRequest("x", "x@t.test", "Passw0rd-1", UserRole.SystemAdmin)));
        Assert.True(ex.Errors.ContainsKey("role"));
        Assert.Equal(Messages.InvalidRole.Text, ex.Message);
    }

    [Fact]
    public async Task LastActiveAdmin_CannotBeDemotedOrDisabled()
    {
        var (service, users, _, admin) = Users(currentUserId: Guid.NewGuid());

        var demote = await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.UpdateAsync(TenantId, admin.Id, new UpdateUserRequest(admin.FullName, UserRole.Merchant, true, null)));
        Assert.Equal(Messages.LastAdmin.Text, demote.Message);

        var disable = await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.UpdateAsync(TenantId, admin.Id, new UpdateUserRequest(admin.FullName, UserRole.TenantAdmin, false, null)));
        Assert.Equal("isActive", disable.Field);

        Assert.Equal(UserRole.TenantAdmin, users.Users.Single(u => u.Id == admin.Id).Role);
    }

    [Fact]
    public async Task InactiveOrOtherTenantsAdmins_DoNotCountAsTheRemainingAdmin()
    {
        var (service, users, _, admin) = Users(currentUserId: Guid.NewGuid());
        users.Users.Add(NewUser(UserRole.TenantAdmin, active: false));
        var foreign = NewUser(UserRole.TenantAdmin);
        foreign.TenantId = Guid.NewGuid();
        users.Users.Add(foreign);

        await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.UpdateAsync(TenantId, admin.Id, new UpdateUserRequest(admin.FullName, UserRole.Operator, true, null)));
    }

    [Fact]
    public async Task Admin_CannotDemoteOrDisableThemself_EvenWithAnotherAdmin()
    {
        var (service, users, _, admin) = Users();
        users.Users.Add(NewUser(UserRole.TenantAdmin));

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.UpdateAsync(TenantId, admin.Id, new UpdateUserRequest(admin.FullName, UserRole.Merchant, true, null)));
        Assert.Equal(Messages.SelfDemote.Text, ex.Message);

        await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.UpdateAsync(TenantId, admin.Id, new UpdateUserRequest(admin.FullName, UserRole.TenantAdmin, false, null)));
    }

    [Fact]
    public async Task WithAnotherActiveAdmin_DemotionApplies_AndBringsTheNewRolesDefaults()
    {
        var (service, users, _, admin) = Users(currentUserId: Guid.NewGuid());
        users.Users.Add(NewUser(UserRole.TenantAdmin));

        var result = await service.UpdateAsync(TenantId, admin.Id,
            new UpdateUserRequest(admin.FullName, UserRole.Merchant, true, null));

        Assert.Equal("Merchant", result.User.Role);
        Assert.NotNull(result.RotatedSecurityStamp);
        Assert.Equal((UiMode.Simple, TextScale.Large), (admin.UiMode, admin.TextScale));
    }

    [Theory]
    [InlineData("")]
    [InlineData("        ")]
    [InlineData("1234567")]
    public async Task Create_WithAShortPassword_IsValidation_AndAddsNobody(string password)
    {
        var (service, users, audit, _) = Users();

        var ex = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.CreateAsync(TenantId, new CreateUserRequest("انباردار", "op@t.test", password, UserRole.Operator)));

        Assert.Equal(PasswordPolicy.TooShortMessage, Assert.Single(ex.Errors["password"]));
        Assert.Contains("۸", ex.Message);
        Assert.Single(users.Users); // فقط همان تنظیم‌کنندهٔ آزمون
        Assert.Empty(audit.Records);
    }

    [Fact]
    public async Task AdminReset_WithAShortPassword_IsValidation_AndChangesNothing()
    {
        var (service, users, _, _) = Users();
        var merchant = NewUser(UserRole.Merchant);
        users.Users.Add(merchant);
        var (hash, stamp) = (merchant.PasswordHash, merchant.SecurityStamp);

        await Assert.ThrowsAsync<AppValidationException>(() =>
            service.UpdateAsync(TenantId, merchant.Id, new UpdateUserRequest("نام تازه", UserRole.Merchant, true, "short")));

        Assert.Equal((merchant.Role.ToString(), hash, stamp), (merchant.FullName, merchant.PasswordHash, merchant.SecurityStamp));

        // رمز خالی یعنی «بدون تغییر» و قاعده را نمی‌خورد؛ رمز درست بازنشانی می‌شود و مهر می‌چرخد
        await service.UpdateAsync(TenantId, merchant.Id, new UpdateUserRequest("نام تازه", UserRole.Merchant, true, null));
        Assert.Equal(hash, merchant.PasswordHash);

        var result = await service.UpdateAsync(TenantId, merchant.Id,
            new UpdateUserRequest("نام تازه", UserRole.Merchant, true, "Passw0rd-reset"));
        Assert.NotEqual(hash, merchant.PasswordHash);
        Assert.Equal(merchant.SecurityStamp, result.RotatedSecurityStamp);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    public void PasswordPolicy_NeedsEightCharacters(string? password, bool acceptable)
        => Assert.Equal(acceptable, PasswordPolicy.IsAcceptable(password));

    [Fact]
    public async Task NameOnlyEdit_KeepsTheUsersOwnTextScale()
    {
        var (service, users, _, _) = Users();
        var merchant = NewUser(UserRole.Merchant);
        merchant.TextScale = TextScale.XLarge;
        users.Users.Add(merchant);

        await service.UpdateAsync(TenantId, merchant.Id, new UpdateUserRequest("نام تازه", UserRole.Merchant, true, null));

        Assert.Equal(TextScale.XLarge, merchant.TextScale);
    }

    // ═════════════════════════════════════════════════════════════════════
    // ترجیحات کاربر
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MyPreferences_TextScaleAndGridEnter_AreSaved()
    {
        var users = new FakeUserRepository();
        var merchant = NewUser(UserRole.Merchant);
        users.Users.Add(merchant);
        var uow = new FakeUnitOfWork();
        var service = new UserPreferencesService(users, uow);

        var dto = await service.UpdateAsync(merchant.Id, new UpdateMyPreferencesRequest(TextScale.XLarge, GridEnterDirection.Right));

        Assert.Equal((TextScale.XLarge, GridEnterDirection.Right), (dto.TextScale, dto.GridEnter));
        Assert.Equal(1, uow.Saves);

        // بدون تغییر، ذخیره‌ای هم نیست
        await service.UpdateAsync(merchant.Id, new UpdateMyPreferencesRequest(TextScale.XLarge));
        Assert.Equal(1, uow.Saves);
    }

    [Theory]
    [InlineData(UserRole.Merchant, UiMode.Simple)]
    [InlineData(UserRole.Operator, UiMode.Simple)]
    [InlineData(UserRole.TenantAdmin, UiMode.Simple)]
    public async Task MyPreferences_UiMode_IsHonouredOnlyForTenantAdmin(UserRole role, UiMode requested)
    {
        var users = new FakeUserRepository();
        var user = NewUser(role);
        users.Users.Add(user);
        var before = user.UiMode;

        var dto = await new UserPreferencesService(users, new FakeUnitOfWork())
            .UpdateAsync(user.Id, new UpdateMyPreferencesRequest(UiMode: role == UserRole.TenantAdmin ? requested : UiMode.Full));

        Assert.Equal(role == UserRole.TenantAdmin ? requested : before, dto.UiMode);
    }

    [Fact]
    public async Task MyPreferences_DismissCoach_AppendsOnce_AndRejectsJunkKeys()
    {
        var users = new FakeUserRepository();
        var user = NewUser(UserRole.Merchant);
        users.Users.Add(user);
        var service = new UserPreferencesService(users, new FakeUnitOfWork());

        await service.UpdateAsync(user.Id, new UpdateMyPreferencesRequest(DismissCoach: "home.tiles"));
        var dto = await service.UpdateAsync(user.Id, new UpdateMyPreferencesRequest(DismissCoach: " home.tiles "));
        Assert.Equal(new[] { "home.tiles" }, dto.CoachDismissed);

        var ex = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.UpdateAsync(user.Id, new UpdateMyPreferencesRequest(DismissCoach: "<script>")));
        Assert.True(ex.Errors.ContainsKey("dismissCoach"));

        await Assert.ThrowsAsync<AppValidationException>(() =>
            service.UpdateAsync(user.Id, new UpdateMyPreferencesRequest((TextScale)9)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1, \"x\", null, \"x\"]")]
    public void ParseCoach_NeverThrows(string? json)
    {
        var keys = UserPreferencesService.ParseCoach(json);
        Assert.True(keys.Count <= 1);
        Assert.All(keys, k => Assert.Equal("x", k));
    }

    [Fact]
    public async Task Me_CarriesRoleLabelAndPreferences()
    {
        var users = new FakeUserRepository();
        var merchant = NewUser(UserRole.Merchant);
        users.Users.Add(merchant);

        var me = await new UserPreferencesService(users, new FakeUnitOfWork()).GetMeAsync(merchant.Id);

        Assert.Equal("بازرگان", me.RoleLabel);
        Assert.Equal(TextScale.Large, me.Preferences.TextScale);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new UserPreferencesService(users, new FakeUnitOfWork()).GetMeAsync(Guid.NewGuid()));
    }

    // ═════════════════════════════════════════════════════════════════════
    // ترجیحات کارگاه
    // ═════════════════════════════════════════════════════════════════════

    private static TenantPreferencesDto DefaultsDto(uint version = 0) => new(
        "RUG", SerialMode.SuggestNext, null, 0, null, AllocationMethod.ByArea, AreaBasis.Effective, "rug.origin", null,
        null, 10, true, BaleTerm.Negleh, null, null, null, null, null, null, false, version);

    [Fact]
    public async Task TenantPreferences_MissingRow_ReadsAsDefaultsWithVersionZero()
    {
        var repo = new FakeTenantPreferencesRepository { HasMoney = true };
        var dto = await new TenantPreferencesService(repo, new FakeAuditLog(), new FakeUnitOfWork()).GetAsync(TenantId);

        Assert.Equal(0u, dto.Version);
        Assert.Equal("RUG", dto.SkuPrefix);
        Assert.Equal(12m, dto.MaxPlausibleSideM);
        Assert.True(dto.MoneyScaleLocked);
    }

    [Fact]
    public async Task TenantPreferences_Update_NormalizesAndAudits_WithTheExpectedVersion()
    {
        var repo = new FakeTenantPreferencesRepository { Row = TenantPreferences.CreateDefault(TenantId), Version = 41 };
        var audit = new FakeAuditLog();
        var uow = new FakeUnitOfWork();
        var service = new TenantPreferencesService(repo, audit, uow);

        var dto = await service.UpdateAsync(TenantId, DefaultsDto(41) with
        {
            SkuPrefix = " ker ",
            BaleTerm = BaleTerm.Adl,
            DefaultBaleTargetAreaM2 = 13m,
            ExporterName = "  شركت  نمونه "
        });

        Assert.Equal(41u, repo.ExpectedVersion);
        Assert.Equal(1, uow.Saves);
        Assert.Equal("KER", dto.SkuPrefix);
        Assert.Equal(BaleTerm.Adl, dto.BaleTerm);
        Assert.Equal("شرکت نمونه", dto.ExporterName); // ك عربی و فاصلهٔ اضافه تمیز شد
        var record = Assert.Single(audit.Records);
        Assert.Equal(AuditAction.PreferencesChanged, record.Action);
        Assert.Contains("عدل", record.Summary);
    }

    [Fact]
    public async Task TenantPreferences_NoChange_DoesNotSave()
    {
        var repo = new FakeTenantPreferencesRepository { Row = TenantPreferences.CreateDefault(TenantId) };
        var uow = new FakeUnitOfWork();
        var audit = new FakeAuditLog();

        await new TenantPreferencesService(repo, audit, uow).UpdateAsync(TenantId, DefaultsDto(repo.Version));

        Assert.Equal(0, uow.Saves);
        Assert.Empty(audit.Records);
    }

    [Fact]
    public async Task TenantPreferences_InvalidFields_AreAllReportedAtOnce()
    {
        var repo = new FakeTenantPreferencesRepository { Row = TenantPreferences.CreateDefault(TenantId) };
        var service = new TenantPreferencesService(repo, new FakeAuditLog(), new FakeUnitOfWork());

        var ex = await Assert.ThrowsAsync<AppValidationException>(() => service.UpdateAsync(TenantId, DefaultsDto(repo.Version) with
        {
            SkuPrefix = "رگ-۱",
            MoneyScale = 3,
            MaxPlausibleSideM = 40m,
            SerialStart = 0,
            DeclaredValueKeyField = "",
            BaleTargetTolerancePercent = 101,
            DefaultUsdFx = -1m,
            BaleTerm = (BaleTerm)7
        }));

        Assert.Equal(
            new[] { "baleTargetTolerancePercent", "baleTerm", "declaredValueKeyField", "defaultUsdFx", "maxPlausibleSideM", "moneyScale", "serialStart", "skuPrefix" },
            ex.Errors.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.All(ex.Errors.Values.SelectMany(m => m), m => Assert.DoesNotMatch(LatinLetter(), m));
    }

    [Fact]
    public async Task TenantPreferences_MoneyScale_IsLockedOnceMoneyExists()
    {
        var repo = new FakeTenantPreferencesRepository { Row = TenantPreferences.CreateDefault(TenantId), HasMoney = true };
        var service = new TenantPreferencesService(repo, new FakeAuditLog(), new FakeUnitOfWork());

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() =>
            service.UpdateAsync(TenantId, DefaultsDto(repo.Version) with { MoneyScale = 2 }));
        Assert.Equal("moneyScale", ex.Field);
        Assert.Equal(Messages.MoneyLocked.Text, ex.Message);

        // بقیهٔ ترجیحات هنوز ذخیره می‌شوند
        var dto = await service.UpdateAsync(TenantId, DefaultsDto(repo.Version) with { BaleTerm = BaleTerm.Adl });
        Assert.True(dto.MoneyScaleLocked);
    }

    [Fact]
    public async Task TenantPreferences_MissingRow_IsCreatedOnlyFromVersionZero()
    {
        var repo = new FakeTenantPreferencesRepository();
        var service = new TenantPreferencesService(repo, new FakeAuditLog(), new FakeUnitOfWork());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.UpdateAsync(TenantId, DefaultsDto(5)));
        Assert.Equal(0, repo.Adds);

        await service.UpdateAsync(TenantId, DefaultsDto(0) with { SerialStart = 3778 });
        Assert.Equal(1, repo.Adds);
        Assert.Equal(3778, repo.Row!.SerialStart);
    }

    [Fact]
    public async Task TenantPreferences_DeclarationTemplate_IsNotWrittenFromThisEndpoint()
    {
        var repo = new FakeTenantPreferencesRepository { Row = TenantPreferences.CreateDefault(TenantId) };
        await new TenantPreferencesService(repo, new FakeAuditLog(), new FakeUnitOfWork())
            .UpdateAsync(TenantId, DefaultsDto(repo.Version) with { DefaultDeclarationTemplateId = Guid.NewGuid() });
        Assert.Null(repo.Row!.DefaultDeclarationTemplateId);
    }

    [Fact]
    public async Task SeedContributor_AddsTheRowOnce()
    {
        var repo = new FakeTenantPreferencesRepository();
        var contributor = new TenantPreferencesSeedContributor(repo);

        await contributor.SeedAsync(TenantId, CancellationToken.None);
        await contributor.SeedAsync(TenantId, CancellationToken.None);

        Assert.Equal(1, repo.Adds);
        Assert.Equal(100, contributor.Order);
    }

    // ═════════════════════════════════════════════════════════════════════
    // پول در سرویس فرش (A.5، E5)
    // ═════════════════════════════════════════════════════════════════════

    private static (RugManagementService Service, FakeRugRepository Rugs, Rug Rug) RugService(bool canSeeMoney)
    {
        var rugs = new FakeRugRepository();
        var rug = new Rug
        {
            TenantId = TenantId,
            Sku = "RUG-202609-0001",
            Title = "قدیمی",
            WidthMeters = 2m,
            LengthMeters = 3m,
            PurchaseCost = 5_000_000m,
            TargetSalePrice = 9_000_000m
        };
        rugs.Rugs[rug.Id] = rug;

        var service = new RugManagementService(
            rugs, null!, new WorkflowEngine(new CostCalculationService(), new EmptyStepTypeLookup()), null!, null!, null!,
            null!, new FakeCurrentUser(), new FakeAuditLog(), new FakeUnitOfWork(),
            new MoneyRedactionTests.Visibility(canSeeMoney));
        return (service, rugs, rug);
    }

    private static UpdateRugRequest Update(decimal? purchaseCost, decimal? targetSalePrice) => new(
        "تازه", null, null, null, null, 2m, 3m, purchaseCost, targetSalePrice, null, null, null);

    [Fact]
    public async Task OperatorEdit_WithNullMoney_KeepsTheStoredPrices_AndReturnsNoMoney()
    {
        var (service, _, rug) = RugService(canSeeMoney: false);

        var dto = await service.UpdateAsync(TenantId, rug.Id, Update(null, null));

        Assert.Equal("تازه", rug.Title);
        Assert.Equal(5_000_000m, rug.PurchaseCost);
        Assert.Equal(9_000_000m, rug.TargetSalePrice);
        Assert.Null(dto.PurchaseCost);
        Assert.Null(dto.Costs);
    }

    [Fact]
    public async Task OperatorEdit_WithAPrice_Is403_AndChangesNothing()
    {
        var (service, _, rug) = RugService(canSeeMoney: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(TenantId, rug.Id, Update(1m, null)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.BulkUpdateFieldsAsync(TenantId, new BulkUpdateFieldsRequest([rug.Id], null, null, null, null, 1m, null, null)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AdvanceStepAsync(TenantId, rug.Id, Guid.NewGuid(), new AdvanceRugStepRequest(null, 10m, null, null, null, null, null)));

        Assert.Equal("قدیمی", rug.Title);
        Assert.Equal(5_000_000m, rug.PurchaseCost);
    }

    [Fact]
    public async Task MerchantEdit_WritesAndSeesMoney()
    {
        var (service, _, rug) = RugService(canSeeMoney: true);

        var dto = await service.UpdateAsync(TenantId, rug.Id, Update(6_000_000m, null));

        Assert.Equal(6_000_000m, rug.PurchaseCost);
        Assert.Equal(6_000_000m, dto.PurchaseCost);
        Assert.Equal(6_000_000m, dto.Costs!.TotalInvestment);
    }

    [Fact]
    public async Task OperatorSearch_SortsByCostAsCreatedAt_AndHidesInvestment()
    {
        var (service, rugs, _) = RugService(canSeeMoney: false);
        rugs.Page = new PagedResult<RugListItemDto>(
            [new RugListItemDto(Guid.NewGuid(), "S", null, null, null, 2m, 3m, 6m, RugStatus.Draft, null, null, null, null, null,
                7m, 0, 0, DateTimeOffset.UtcNow)], 1, 1, 25);

        var page = await service.SearchAsync(TenantId, new RugQuery { SortBy = RugSortBy.TotalCost });

        Assert.Equal(RugSortBy.CreatedAt, rugs.LastQuery!.SortBy);
        Assert.Null(Assert.Single(page.Items).TotalInvestment);
    }

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinLetter();
}
