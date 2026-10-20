using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Application.Common;

/// <summary>
/// The single source of truth for the demo payment policy. With only two roles
/// (Admin and read-only User), risk determines the workflow rather than a deeper
/// role hierarchy: low-risk payments are auto-approved by the system; everything
/// else requires a different Admin (maker-checker).
/// </summary>
public static class PaymentPolicy
{
    public const decimal AutoApprovalLimit = 50_000m;
    public const decimal MediumRiskLimit = 250_000m;
    public const decimal HighRiskLimit = 1_000_000m;

    public static PaymentRiskLevel RiskFor(decimal amount)
    {
        if (amount >= HighRiskLimit) return PaymentRiskLevel.Critical;
        if (amount >= MediumRiskLimit) return PaymentRiskLevel.High;
        if (amount >= AutoApprovalLimit) return PaymentRiskLevel.Medium;
        return PaymentRiskLevel.Low;
    }

    public static bool RequiresMakerChecker(decimal amount) => amount >= AutoApprovalLimit;

    public static string DescribeRisk(PaymentRiskLevel risk) => risk switch
    {
        PaymentRiskLevel.Critical => "Critical value payment. Requires an Admin checker and balance verification.",
        PaymentRiskLevel.High => "High value payment. Requires an Admin checker.",
        PaymentRiskLevel.Medium => "Medium risk payment. Requires an Admin checker.",
        _ => "Low risk payment auto-approved by system policy."
    };
}
