using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests.P4a;

/// <summary>
/// عکس «قطعی» (spec C.7، L.2 «Snapshot»): متن متعارف (کلیدهای مرتب، عدد با رقم ثابت، NFC، ترتیب ثابت آرایه‌ها)، هش
/// SHA-256 همان بایت‌ها، پایدار در «قطعی» دوباره، بی هیچ مبلغ تومانی؛ و قاعده‌های آمادگی (C.6) با شمار بازدارنده‌های فهرست.
/// </summary>
public class SnapshotAndReadinessTests
{
    private static readonly Guid BaleOne = Guid.Parse("00000000-0000-0000-0000-00000000b001");
    private static readonly Guid BaleTwo = Guid.Parse("00000000-0000-0000-0000-00000000b002");

    private static ShipmentHeaderData Header(string title = "اظهار ۸")
        => new(Guid.Parse("00000000-0000-0000-0000-000000005001"), 8, title, new DateOnly(2026, 9, 6), "هامبورگ", null, "۱۲۳",
            null, ShipmentStatus.Open, 0, null, null, 13m, 105_000.5m, "یادداشت", 77, DateTimeOffset.UnixEpoch, null);

    private static ShipmentRowData Row(
        Guid bale, int baleNumber, int seq, string serial, int qty, decimal area, decimal? lineUsd, decimal? finalLength = 2.08m,
        string origin = "گبه", DeclaredRateSource source = DeclaredRateSource.Table, string? metadata = null)
        => new(Guid.NewGuid(), 1, seq, null, bale, baleNumber,
            Guid.Parse($"00000000-0000-0000-0000-{seq:D4}{baleNumber:D8}"), 1, "RUG-" + serial, serial, qty,
            "گبه", origin, "ساده", "پشم", null, metadata,
            2.08m, 2.10m, finalLength, finalLength is null ? null : 2.10m, area,
            true, lineUsd is null ? null : 45m, source, lineUsd is null ? null : Math.Round(lineUsd.Value / qty, 2), area * qty, lineUsd,
            origin);

    /// <summary>D.7 #9: نگلهٔ ۱ = A و B، نگلهٔ ۲ = C؛ گبه ۴۵ دلار.</summary>
    private static SnapshotInput Example(IEnumerable<ShipmentRowData>? rows = null, string title = "اظهار ۸")
    {
        var all = (rows ?? [
            Row(BaleOne, 1, 1, "3778", 1, 4.37m, 196.65m),
            Row(BaleOne, 1, 2, "3911 - 3960", 50, 0.16m, 360.00m),
            Row(BaleTwo, 2, 1, "3782", 1, 8.25m, 371.25m, metadata: """{"bg_color":"لاکی","secret":"x"}""")
        ]).ToList();
        var bales = new List<BaleData>
        {
            new(BaleOne, 1, 1, 120.5m, null, 2, 51, 12.37m, 556.65m),
            new(BaleTwo, 2, 1, null, null, 1, 1, 8.25m, 371.25m)
        };
        var preferences = TenantPreferences.CreateDefault(Guid.NewGuid());
        preferences.ExporterName = "صادرکننده";
        return new SnapshotInput(Header(title), preferences, "rug.origin", bales, all,
            [new CustomFieldInfo("bg_color", "رنگ زمینه", true), new CustomFieldInfo("secret", "یادداشت داخلی", false)]);
    }

    [Fact]
    public void Snapshot_IsCanonical_AndHashedOverItsUtf8Bytes()
    {
        var json = SnapshotBuilder.Build(Example());
        var hash = SnapshotBuilder.Hash(json);

        Assert.Equal(64, hash.Length);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))), hash);
        Assert.Equal(json.Normalize(NormalizationForm.FormC), json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        AssertSortedKeys(root);

        // عددها با رقم ثابت، خام در JSON
        Assert.Contains("\"usdRate\":105000.500000", json);
        Assert.Contains("\"areaM2\":20.62", json);
        Assert.Contains("\"usdTotal\":927.90", json);
        var totals = root.GetProperty("totals");
        Assert.Equal(52, totals.GetProperty("pieces").GetInt32());
        Assert.Equal(3, totals.GetProperty("lines").GetInt32());

        var bales = root.GetProperty("bales").EnumerateArray().ToList();
        Assert.Equal([1, 2], bales.Select(b => b.GetProperty("number").GetInt32()));
        Assert.Equal("556.65", bales[0].GetProperty("usdTotal").GetRawText());
        Assert.Equal("120.50", bales[0].GetProperty("weightKg").GetRawText());
        Assert.Equal(["3778", "3911 - 3960"], bales[0].GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("serial").GetString()));

        // فقط فیلدهای سفارشی «خروجی‌پذیر»
        var custom = bales[1].GetProperty("rows")[0].GetProperty("custom");
        Assert.Equal("لاکی", custom.GetProperty("bg_color").GetString());
        Assert.False(custom.TryGetProperty("secret", out _));
        Assert.Equal("table", bales[1].GetProperty("rows")[0].GetProperty("rateSource").GetString());
    }

    [Fact]
    public void Snapshot_IsStable_RegardlessOfInputOrder_AndOfNonExportedState()
    {
        var first = SnapshotBuilder.Build(Example());
        var reversed = Example();
        reversed = reversed with { Rows = reversed.Rows.Reverse().ToList(), Bales = reversed.Bales.Reverse().ToList() };
        // شمار قطعی، زمان، کاربر، نسخه و یادداشت در عکس نیستند
        var refinalized = reversed with
        {
            Header = reversed.Header with { FinalizeCount = 3, FinalizedAt = DateTimeOffset.UtcNow, Version = 999, Notes = "دیگر" }
        };

        Assert.Equal(first, SnapshotBuilder.Build(refinalized));
        Assert.Equal(SnapshotBuilder.Hash(first), SnapshotBuilder.Hash(SnapshotBuilder.Build(refinalized)));
        Assert.NotEqual(SnapshotBuilder.Hash(first), SnapshotBuilder.Hash(SnapshotBuilder.Build(Example(title: "اظهار دیگر"))));
    }

    [Fact]
    public void Snapshot_HasNoTomanMoney()
    {
        var json = SnapshotBuilder.Build(Example());
        foreach (var key in new[] { "purchase", "cost", "price", "investment", "revenue", "profit", "toman" })
            Assert.DoesNotContain(key, json, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSortedKeys(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var names = element.EnumerateObject().Select(p => p.Name).ToList();
                Assert.Equal(names.Order(StringComparer.Ordinal), names);
                foreach (var property in element.EnumerateObject()) AssertSortedKeys(property.Value);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) AssertSortedKeys(item);
                break;
        }
    }

    // ── آمادگی ─────────────────────────────────────────────────────────

    private static ReadinessSettings Settings(bool requireFinal = true, decimal? target = 13m)
        => new(requireFinal, target, 10, BaleTerms.Negleh, "محل بافت");

    [Fact]
    public void EmptyShipment_IsBlocking()
    {
        var issue = Assert.Single(ReadinessRules.Build([], [], Settings()));
        Assert.Equal(("E_EMPTY_SHIPMENT", true), (issue.Code, issue.Blocking));
    }

    [Fact]
    public void EveryRule_ProducesItsChip_InAFixedOrder()
    {
        var rugNoFinal = Guid.NewGuid();
        var rugKashan1 = Guid.NewGuid();
        var rugKashan2 = Guid.NewGuid();
        var rugNoKey = Guid.NewGuid();
        var rugNoSerial = Guid.NewGuid();
        ReadinessBale[] bales =
        [
            new(Guid.NewGuid(), 1, 3, 15.10m),
            new(Guid.NewGuid(), 5, 0, 0m),
            new(Guid.NewGuid(), 2, 2, 10m)
        ];
        ReadinessRow[] rows =
        [
            new(rugNoFinal, "1", false, DeclaredRateSource.Table, "گبه"),
            new(rugKashan1, "2", true, DeclaredRateSource.Missing, "کاشان"),
            new(rugKashan2, "3", true, DeclaredRateSource.Missing, "كاشان"),
            new(rugNoKey, "4", true, DeclaredRateSource.Missing, null),
            new(rugNoSerial, null, true, DeclaredRateSource.Override, "گبه")
        ];

        var issues = ReadinessRules.Build(bales, rows, Settings());

        Assert.Equal(
            ["E_EMPTY_BALE", "E_NO_FINAL_SIZE", "E_NO_DECLARED_VALUE", "E_NO_DECLARED_KEY", "W_BALE_OVER_TARGET", "W_NO_SERIAL"],
            issues.Select(i => i.Code));
        Assert.Equal("نگلهٔ ۵ خالی است؛ یا فرش در آن بگذارید یا حذفش کنید.", issues[0].Message);
        Assert.Equal(5, issues[0].BaleNumber);
        Assert.Equal([rugNoFinal], issues[1].RugIds);
        Assert.Equal("ارزش گمرکی ۲ فرش «کاشان» معلوم نیست.", issues[2].Message);
        Assert.Equal("کاشان", issues[2].Key);
        Assert.Equal(new HashSet<Guid> { rugKashan1, rugKashan2 }, issues[2].RugIds.ToHashSet());
        Assert.Equal("نگلهٔ ۱ شد ۱۵٫۱ متر؛ از ۱۳ متر هدف بیشتر است.", issues[4].Message);
        Assert.False(issues[4].Blocking);
        Assert.Equal([rugNoSerial], issues[5].RugIds);

        // شمار بازدارنده‌های فهرست همان Build است (یک SQL گروهی، C.6)
        Assert.Equal(issues.Count(i => i.Blocking),
            ReadinessRules.BlockingCount(rows.Length, 1, 1, requireFinalSize: true, ["کاشان", "كاشان", null]));
    }

    [Fact]
    public void NoFinalSize_IsAWarning_WhenThePreferenceAllowsIt()
    {
        ReadinessRow[] rows = [new(Guid.NewGuid(), "1", false, DeclaredRateSource.Table, "گبه")];
        var issue = Assert.Single(ReadinessRules.Build([new ReadinessBale(Guid.NewGuid(), 1, 1, 4m)], rows, Settings(requireFinal: false)));
        Assert.Equal(("E_NO_FINAL_SIZE", false), (issue.Code, issue.Blocking));
        Assert.Equal(0, ReadinessRules.BlockingCount(1, 0, 1, requireFinalSize: false, []));
    }
}
