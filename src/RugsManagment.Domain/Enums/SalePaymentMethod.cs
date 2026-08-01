namespace RugsManagment.Domain.Enums;

/// <summary>نحوهٔ پرداخت در فروش فرش.</summary>
public enum SalePaymentMethod
{
    /// <summary>نقدی</summary>
    Cash = 0,

    /// <summary>کارت‌خوان / کارت به کارت</summary>
    Card = 1,

    /// <summary>حواله یا انتقال بانکی</summary>
    Transfer = 2,

    /// <summary>چک</summary>
    Cheque = 3,

    /// <summary>اقساطی — مبلغ دریافتی ممکن است کمتر از مبلغ فروش باشد</summary>
    Installment = 4
}
