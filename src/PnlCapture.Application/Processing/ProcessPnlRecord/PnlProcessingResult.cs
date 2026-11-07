using PnlCapture.Domain.Entities;

namespace PnlCapture.Application.Processing.ProcessPnlRecord;

public sealed record PnlProcessingResult(PnlRecord? Record, bool IsDuplicate);
