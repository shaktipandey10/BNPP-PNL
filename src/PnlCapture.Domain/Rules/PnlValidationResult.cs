using PnlCapture.Domain.Enums;

namespace PnlCapture.Domain.Rules;

public readonly record struct PnlValidationResult(
    ValidationStatus Status,
    string? Reason);
