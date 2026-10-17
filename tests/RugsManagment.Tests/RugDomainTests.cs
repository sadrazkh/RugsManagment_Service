using RugsManagment.Domain.Common;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// فرش نسخهٔ ۲ (spec B.4، L.1 «Rug domain»): اندازهٔ نهایی بر اندازهٔ اول غالب است، پاک کردنش برمی‌گرداند، هر دو ضلع با
/// هم؛ مساحت دسته؛ شماره و تعداد با هم؛ قاعدهٔ I.3 برای setter قدیمی جفت مؤثر. و دیگر موجودیت‌های همین فاز.
/// </summary>
public class RugDomainTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 6, 8, 0, 0, TimeSpan.Zero);

    private static Rug NewRug(decimal length = 2.08m, decimal width = 2.10m)
    {
        var rug = new Rug { TenantId = Guid.NewGuid(), Sku = "RUG-1" };
        rug.SetIntakeSize(length, width);
        return rug;
    }

    [Fact]
    public void IntakeSize_IsTheEffectiveSize_UntilAFinalSizeExists()
    {
        var rug = NewRug(3.35m, 2.52m);
        Assert.Equal(3.35m, rug.LengthMeters);
        Assert.Equal(2.52m, rug.WidthMeters);
        Assert.False(rug.HasFinalSize);
        Assert.Equal(8.44m, rug.AreaM2);
        Assert.Equal(8.44m, rug.IntakeAreaM2);
    }

    [Fact]
    public void FinalSize_OverridesIntake_AndClearReverts()
    {
        var rug = NewRug(3.35m, 2.52m);
        var user = Guid.NewGuid();

        rug.SetFinalSize(3.30m, 2.50m, At, user);
        Assert.True(rug.HasFinalSize);
        Assert.Equal((3.30m, 2.50m), (rug.LengthMeters, rug.WidthMeters));
        Assert.Equal((3.35m, 2.52m), (rug.IntakeLength, rug.IntakeWidth));
        Assert.Equal(8.25m, rug.AreaM2);
        Assert.Equal(8.44m, rug.IntakeAreaM2); // قیمت متری خرید روی اندازهٔ اول می‌ماند
        Assert.Equal(At, rug.FinalMeasuredAt);
        Assert.Equal(user, rug.FinalMeasuredBy);

        // اندازهٔ اول عوض شود، اندازهٔ مؤثر همان نهایی می‌ماند
        rug.SetIntakeSize(3.40m, 2.52m);
        Assert.Equal(3.30m, rug.LengthMeters);

        rug.ClearFinalSize();
        Assert.False(rug.HasFinalSize);
        Assert.Null(rug.FinalLength);
        Assert.Null(rug.FinalWidth);
        Assert.Null(rug.FinalMeasuredAt);
        Assert.Null(rug.FinalMeasuredBy);
        Assert.Equal((3.40m, 2.52m), (rug.LengthMeters, rug.WidthMeters));
    }

    [Fact]
    public void SameAsIntake_RecordsAnExplicitFinalSize()
    {
        var rug = NewRug(2.08m, 2.10m);
        rug.SetFinalSizeSameAsIntake(At, null);
        Assert.True(rug.HasFinalSize);
        Assert.Equal(rug.IntakeLength, rug.FinalLength);
        Assert.Equal(rug.IntakeWidth, rug.FinalWidth);
    }

    [Theory]
    [InlineData("0.04", "2")]
    [InlineData("2", "30.01")]
    [InlineData("2.085", "2")]
    public void Sizes_OutsideTheHardLimits_AreRejectedWithoutChangingTheRug(string length, string width)
    {
        var rug = NewRug(2.08m, 2.10m);
        var l = decimal.Parse(length, System.Globalization.CultureInfo.InvariantCulture);
        var w = decimal.Parse(width, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<DomainValidationException>(() => rug.SetIntakeSize(l, w));
        Assert.Throws<DomainValidationException>(() => rug.SetFinalSize(l, w, At, null));
        Assert.Equal((2.08m, 2.10m), (rug.IntakeLength, rug.IntakeWidth));
        Assert.False(rug.HasFinalSize);
    }

    [Fact]
    public void TotalArea_OfALot_IsPieceAreaTimesQuantity()
    {
        var rug = NewRug(0.40m, 0.40m);
        rug.SetSerialAndQuantity(SerialNumber.Parse("3911 - 3960"), 50);

        Assert.Equal(0.16m, rug.AreaM2);
        Assert.Equal(8.00m, rug.TotalAreaM2);
        Assert.Equal("3911 - 3960", rug.SerialNo);
        Assert.Equal((3911, 3960), (rug.SerialFrom!.Value, rug.SerialTo!.Value));
        Assert.Equal(3911L, rug.SerialSort);
        Assert.Equal(SerialNumber.Range(3911, 3960), rug.Serial);
    }

    [Fact]
    public void SerialAndQuantity_ChangeTogether_InEitherDirection()
    {
        var rug = NewRug();
        rug.SetSerialAndQuantity(SerialNumber.Parse("3778"), 1);

        // «3778» × ۱ ← «3911 - 3960» × ۵۰ در یک قدم (دو قدم جدا یکی از آن‌ها را رد می‌کرد)
        rug.SetSerialAndQuantity(SerialNumber.Parse("3911 - 3960"), 50);
        Assert.Equal(50, rug.Quantity);

        rug.SetSerialAndQuantity(SerialNumber.Parse("3778"), 1);
        Assert.Equal(1, rug.Quantity);
        Assert.Equal(3778, rug.SerialTo);
    }

    [Fact]
    public void ARangeShorterThanTheQuantity_IsRejected()
    {
        var rug = NewRug();
        rug.SetSerial(SerialNumber.Parse("3778"));

        var thrown = Assert.Throws<DomainValidationException>(() => rug.SetQuantity(2));
        Assert.True(thrown.Errors.ContainsKey(SerialNumber.Field));
        Assert.Equal(1, rug.Quantity);

        Assert.Throws<DomainValidationException>(() => rug.SetSerialAndQuantity(SerialNumber.Parse("3911 - 3960"), 51));
        Assert.Equal("3778", rug.SerialNo);
    }

    [Fact]
    public void QuantityBelowOne_IsRejected()
    {
        var rug = NewRug();
        var thrown = Assert.Throws<DomainValidationException>(() => rug.SetQuantity(0));
        Assert.Equal(Rug.QuantityErrorMessage, thrown.Message);
        Assert.True(thrown.Errors.ContainsKey("rug.quantity"));
    }

    [Fact]
    public void TextSerial_AllowsAnyQuantity_AndClearingWorks()
    {
        var rug = NewRug();
        rug.SetSerialAndQuantity(SerialNumber.Parse("A-12"), 5);
        Assert.Equal("A-12", rug.SerialNo);
        Assert.Null(rug.SerialFrom);
        Assert.Null(rug.SerialSort);

        rug.SetSerial(null);
        Assert.Null(rug.SerialNo);
        Assert.Null(rug.Serial);
        Assert.Equal(5, rug.Quantity);
    }

    [Fact]
    public void NewRug_HasQuantityOne_AndNoSerial()
    {
        var rug = new Rug();
        Assert.Equal(1, rug.Quantity);
        Assert.Null(rug.Serial);
        Assert.False(rug.HasFinalSize);
    }

    // ── قاعدهٔ I.3 برای setter قدیمی جفت مؤثر ──────────────────────────

    [Fact]
    public void LegacyEffectiveSetter_WritesTheIntakeSize_WhileThereIsNoFinalSize()
    {
        // مسیر قدیمی (RugForm پیش از P2، seed قدیمی) فقط LengthMeters/WidthMeters می‌نویسد
        var rug = new Rug { WidthMeters = 2m, LengthMeters = 3m };
        Assert.Equal((3m, 2m), (rug.IntakeLength, rug.IntakeWidth));
        Assert.Equal(6.00m, rug.AreaM2);

        rug.LengthMeters = 2.675m; // مثل ستون numeric(9,2)، به دو رقم گرد می‌شود
        Assert.Equal(2.68m, rug.LengthMeters);
        Assert.Equal(2.68m, rug.IntakeLength);
    }

    [Fact]
    public void LegacyEffectiveSetter_WithAFinalSize_IgnoresTheSameValue_AndRefusesAnother()
    {
        var rug = NewRug(3.35m, 2.52m);
        rug.SetFinalSize(3.30m, 2.50m, At, null);

        // فرم قدیمی جفت مؤثرِ همان را برمی‌گرداند: بی‌اثر، و اندازهٔ اول را با نهایی بازنویسی نمی‌کند
        rug.LengthMeters = 3.30m;
        rug.WidthMeters = 2.50m;
        Assert.Equal((3.35m, 2.52m), (rug.IntakeLength, rug.IntakeWidth));

        var thrown = Assert.Throws<DomainConflictException>(() => rug.LengthMeters = 3.10m);
        Assert.Equal(Rug.FinalSizeSetErrorMessage, thrown.Message);
        Assert.Equal("rug.length", thrown.Field);
        Assert.Equal(3.30m, rug.LengthMeters);
    }

    [Fact]
    public void AreaSquareMeters_IsTheRoundedPieceArea()
    {
        var rug = NewRug(2.30m, 1.65m);
        Assert.Equal(3.80m, rug.AreaSquareMeters); // 3.795 ← نصفه دور از صفر
    }

    // ── مکان، فروش، مرحله، جمع هزینه ────────────────────────────────────

    [Theory]
    [InlineData(true, true, true, true, RugLocation.Trashed)]
    [InlineData(false, true, true, true, RugLocation.Sold)]
    [InlineData(false, false, true, true, RugLocation.Exported)]
    [InlineData(false, false, true, false, RugLocation.InBale)]
    [InlineData(false, false, false, true, RugLocation.InStock)]
    [InlineData(false, false, false, false, RugLocation.InStock)]
    public void Location_FollowsTheC10Order(bool trashed, bool sold, bool inShipment, bool finalized, RugLocation expected)
        => Assert.Equal(expected, RugLocationRules.Resolve(trashed, sold, inShipment, finalized));

    [Fact]
    public void Sale_ProfitUsesTheSnapshot_WhenThereIsOne()
    {
        var sale = new RugSale { SalePrice = 50_000_000m, Discount = 2_000_000m };
        Assert.Equal(48_000_000m - 40_000_000m, sale.ActualProfit(40_000_000m));

        sale.CostAtSale = 39_000_000m;
        Assert.Equal(9_000_000m, sale.ActualProfit(40_000_000m)); // هزینهٔ بعد از فروش سود را عوض نمی‌کند
    }

    [Fact]
    public void Step_EffectiveCost_IsTheRugCostMathFormula()
    {
        var step = new RugWorkflowStep { CalculatedCost = 100m, Adjustment = -500m };
        Assert.Equal(0m, step.EffectiveCost);
        step.ManualCostOverride = 300m;
        Assert.Equal(RugCostMath.StepEffectiveCost(100m, 300m, -500m), step.EffectiveCost);
    }

    [Fact]
    public void Step_PricingSnapshot_KeepsTwoDecimals()
    {
        var step = new RugWorkflowStep();
        Assert.False(step.HasPricingSnapshot);
        step.SnapshotPricingSize(0.4m, 0.4m, 50);
        Assert.True(step.HasPricingSnapshot);
        Assert.Equal((0.40m, 0.40m, 50), (step.PricedLength!.Value, step.PricedWidth!.Value, step.PricedQuantity!.Value));
        Assert.Throws<ArgumentOutOfRangeException>(() => step.SnapshotPricingSize(1m, 1m, 0));
    }

    [Fact]
    public void CostTotal_ApplyWritesEveryComposedColumn()
    {
        var breakdown = RugCostMath.Compose(15_300_000m, 0m, 0m, 4_524_306m, 8.00m, 50, 0);
        var total = new RugCostTotal { RugId = Guid.NewGuid(), ExportRevenueShare = 5m };
        total.Apply(breakdown, At, 7);

        Assert.Equal(15_300_000m, total.PurchaseCost);
        Assert.Equal(0m, total.StepCost);
        Assert.Equal(4_524_306m, total.ExpenseCost);
        Assert.Equal(19_824_306m, total.Total);
        Assert.Equal(2_478_038m, total.CostPerM2);
        Assert.Equal(396_486m, total.CostPerPiece);
        Assert.Equal(At, total.ComputedAt);
        Assert.Equal(7, total.LedgerVersion);
        Assert.Equal(5m, total.ExportRevenueShare); // سهم درآمد را مرحلهٔ درآمد می‌نویسد، نه ترکیب
    }

    [Fact]
    public void PurchaseLines_OfARug_UseTheIntakeAreaAndQuantity()
    {
        var rug = NewRug(0.40m, 0.40m);
        rug.SetSerialAndQuantity(SerialNumber.Parse("3911 - 3960"), 50);
        rug.SetFinalSize(0.38m, 0.40m, At, null); // اندازهٔ نهایی قیمت خرید را عوض نمی‌کند

        var lines = new List<RugPurchaseLine>
        {
            new() { Basis = CostBasis.PerPiece, Rate = 300_000m },
            new() { Basis = CostBasis.Percent, Rate = 2m }
        };
        Assert.Equal(new[] { 15_000_000m, 300_000m }, RugCostMath.PurchaseLineAmounts(rug, lines, 0));

        lines[0] = new RugPurchaseLine { Basis = CostBasis.PerM2, Rate = 9_000_000m };
        Assert.Equal(new[] { 72_000_000m, 1_440_000m }, RugCostMath.PurchaseLineAmounts(rug, lines, 0));
    }
}
