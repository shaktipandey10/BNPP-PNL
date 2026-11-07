using Microsoft.Extensions.DependencyInjection;
using PnlCapture.Application.Imports.ImportPnlFile;
using PnlCapture.Application.Processing.ProcessPnlRecord;
using PnlCapture.Application.Reports;

namespace PnlCapture.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPnlApplication(this IServiceCollection services)
    {
        services.AddScoped<IPnlProcessingService, PnlProcessingService>();
        services.AddScoped<IImportPnlFileService, ImportPnlFileService>();
        services.AddScoped<IPnlReportService, PnlReportService>();
        return services;
    }
}
