using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence.Ledger;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.Ledger;

/// <summary>
/// برابری C#/SQL (spec D.4 «One formula site»، L.1 «RugCostMath … C#/SQL parity»، L.2): همان بردارهای مشترک
/// (<c>tests/fixtures/math-vectors.json</c>) که <c>RugCostMath</c> را می‌سنجند، این‌جا از راه <see cref="CostSql"/> در پستگرس
/// اجرا می‌شوند — هزینهٔ مؤثر مرحله، جمع مراحل، ترکیب (بها برای هر متر و هر تخته)، و مبلغ سطرهای خرید روی ردیف‌های واقعی؛
/// به‌اضافهٔ ۵۰۰ مرحلهٔ تصادفی.
/// </summary>
public sealed class CostSqlParityTests(LedgerFixture fixture) : IClassFixture<LedgerFixture>
{
    private static decimal D(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static decimal? OptD(JsonElement v, string name)
        => v.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? D(p.GetString()!) : null;

    private static List<JsonElement> Vectors(string op)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "math-vectors.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("costVectors").EnumerateArray()
            .Where(v => v.GetProperty("op").GetString() == op)
            .Select(v => v.Clone())
            .ToList();
    }

    private async Task<decimal?> ScalarAsync(string sql, params object?[] values)
    {
        await using var db = fixture.CreateContext();
        var parameters = values.Select((v, i) => new Npgsql.NpgsqlParameter($"p{i}", NpgsqlTypes.NpgsqlDbType.Numeric) { Value = v ?? DBNull.Value }).ToArray();
        var rows = await db.Database.SqlQueryRaw<decimal?>(sql, parameters).ToListAsync();
        return rows.Single();
    }

    [PostgresFact]
    public async Task StepEffectiveCost_MatchesTheVectorsAndRugCostMath()
    {
        var sql = $"""SELECT {CostSql.StepEffectiveCost("s")} AS "Value" FROM (SELECT @p0::numeric AS "CalculatedCost", @p1::numeric AS "ManualCostOverride", @p2::numeric AS "Adjustment") s""";
        foreach (var v in Vectors("effectiveCost"))
        {
            var (calculated, manual, adjustment) = (OptD(v, "calculated"), OptD(v, "manual"), OptD(v, "adjustment"));
            var sqlValue = await ScalarAsync(sql, calculated, manual, adjustment);
            Assert.Equal(D(v.GetProperty("expected").GetString()!), sqlValue);
            Assert.Equal(RugCostMath.StepEffectiveCost(calculated, manual, adjustment), sqlValue);
        }

        // ۵۰۰ مرحلهٔ تصادفی (L.2 «the provider-ledger SQL step cost vs EffectiveCost over random steps»)
        var random = new Random(20261004);
        for (var i = 0; i < 500; i++)
        {
            decimal? Pick() => random.Next(4) == 0 ? null : Math.Round((decimal)(random.NextDouble() * 2_000_000 - 500_000), random.Next(3));
            var (calculated, manual, adjustment) = (Pick(), random.Next(3) == 0 ? Pick() : null, Pick());
            Assert.Equal(RugCostMath.StepEffectiveCost(calculated, manual, adjustment), await ScalarAsync(sql, calculated, manual, adjustment));
        }
    }

    [PostgresFact]
    public async Task Compose_CostPerM2AndPerPiece_MatchRugCostMath()
    {
        foreach (var v in Vectors("compose"))
        {
            var purchase = D(v.GetProperty("purchaseLines").GetString()!) + D(v.GetProperty("purchaseAllocations").GetString()!);
            var step = D(v.GetProperty("stepCost").GetString()!);
            var expense = D(v.GetProperty("expenseAllocations").GetString()!);
            var area = D(v.GetProperty("totalAreaM2").GetString()!);
            var qty = v.GetProperty("qty").GetInt32();
            var scale = v.GetProperty("scale").GetInt32();

            var totalSql = $"""SELECT {CostSql.Total(CostSql.PurchaseCost("@p0::numeric", "@p1::numeric"), "@p2::numeric", CostSql.ExpenseCost("@p3::numeric"))} AS "Value" """;
            var total = await ScalarAsync(totalSql, D(v.GetProperty("purchaseLines").GetString()!),
                D(v.GetProperty("purchaseAllocations").GetString()!), step, expense);
            var perM2 = await ScalarAsync($"""SELECT {CostSql.CostPerM2("@p0::numeric", "@p1::numeric", "@p2::int")} AS "Value" """, total, area, (decimal)scale);
            var perPiece = await ScalarAsync($"""SELECT {CostSql.CostPerPiece("@p0::numeric", "@p1::int", "@p2::int")} AS "Value" """, total, (decimal)qty, (decimal)scale);

            var expected = RugCostMath.Compose(D(v.GetProperty("purchaseLines").GetString()!),
                D(v.GetProperty("purchaseAllocations").GetString()!), step, expense, area, qty, scale);
            Assert.Equal(expected.Total, total);
            Assert.Equal(D(v.GetProperty("total").GetString()!), total);
            Assert.Equal(expected.CostPerM2, perM2);
            Assert.Equal(OptD(v, "costPerM2"), perM2);
            Assert.Equal(expected.CostPerPiece, perPiece);
            Assert.Equal(purchase, expected.PurchaseCost);
        }
    }

    [PostgresFact]
    public async Task PurchaseLines_OnRealRows_MatchTheVectors()
    {
        foreach (var v in Vectors("purchaseLines"))
        {
            var scale = (short)v.GetProperty("scale").GetInt32();
            var tenant = await fixture.NewTenantAsync(moneyScale: scale);
            var rug = LedgerFixture.NewRug(tenant, D(v.GetProperty("intakeLength").GetString()!), D(v.GetProperty("intakeWidth").GetString()!),
                quantity: v.GetProperty("qty").GetInt32());
            var order = 0;
            foreach (var line in v.GetProperty("lines").EnumerateArray())
                LedgerFixture.Line(rug, Enum.Parse<CostBasis>(line.GetProperty("basis").GetString()!), D(line.GetProperty("rate").GetString()!), order++);

            await fixture.SaveNewAsync(tenant, rug); // PurchaseLineStage با CostSql

            var expected = v.GetProperty("amounts").EnumerateArray().Select(a => D(a.GetString()!)).ToList();
            Assert.Equal(expected, await fixture.LineAmountsAsync(rug.Id));
            Assert.Equal(expected, RugCostMath.PurchaseLineAmounts(rug, rug.PurchaseLines.OrderBy(l => l.SortOrder).ToList(), scale));
            Assert.Equal(D(v.GetProperty("total").GetString()!), (await fixture.TotalAsync(rug.Id))!.PurchaseCost);
        }
    }

    [PostgresFact]
    public async Task StepCost_OverAStatusMix_MatchesTheVector()
    {
        var v = Vectors("stepCost").Single();
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 1m, 1m);
        var index = 0;
        foreach (var s in v.GetProperty("steps").EnumerateArray())
        {
            rug.WorkflowSteps.Add(new Domain.Entities.RugWorkflowStep
            {
                RugId = rug.Id, ProcessStepTypeId = fixture.WashTypeId, OrderIndex = index++,
                Status = Enum.Parse<WorkflowStepStatus>(s.GetProperty("status").GetString()!),
                CalculatedCost = OptD(s, "calculated"), ManualCostOverride = OptD(s, "manual"), Adjustment = OptD(s, "adjustment")
            });
        }

        await fixture.SaveNewAsync(tenant, rug);

        var expected = D(v.GetProperty("expected").GetString()!);
        Assert.Equal(expected, (await fixture.TotalAsync(rug.Id))!.StepCost);
        Assert.Equal(expected, RugCostMath.StepCost(rug.WorkflowSteps));

        await using var scope = fixture.Scope(tenant);
        var report = await scope.ServiceProvider.GetRequiredService<ILedgerVerifier>().VerifyAsync(tenant);
        Assert.Empty(report.Mismatches);
    }
}
