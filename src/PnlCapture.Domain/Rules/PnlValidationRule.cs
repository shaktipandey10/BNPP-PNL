using PnlCapture.Domain.Enums;

namespace PnlCapture.Domain.Rules;

public static class PnlValidationRule
{
    public const string ZeroAmountReason = "PnL amount is zero";

    public static PnlValidationResult Evaluate(decimal pnlAmount)
    {
        return pnlAmount == decimal.Zero
            ? new PnlValidationResult(ValidationStatus.Invalid, ZeroAmountReason)
            : new PnlValidationResult(ValidationStatus.Valid, null);
    }
}
