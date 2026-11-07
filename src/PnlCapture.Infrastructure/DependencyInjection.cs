using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PnlCapture.Application.Abstractions.Files;
using PnlCapture.Application.Abstractions.Persistence;
using PnlCapture.Infrastructure.Files;
using PnlCapture.Infrastructure.Messaging;
using PnlCapture.Infrastructure.Persistence;

namespace PnlCapture.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPnlInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PnlDatabase")
            ?? "Data Source=pnlcapture.db;Default Timeout=5;Cache=Shared";

        services.AddDbContext<PnlDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IPnlRecordRepository, PnlRecordRepository>();
        services.AddSingleton<CsvPnlFileParser>();
        services.AddSingleton<ExcelPnlFileParser>();
        services.AddScoped<IPnlFileParser, PnlFileParser>();
        var rabbitMqSection = configuration.GetSection(RabbitMqOptions.SectionName);
        services.Configure<RabbitMqOptions>(options =>
        {
            options.ConnectionUri = rabbitMqSection[nameof(RabbitMqOptions.ConnectionUri)];
            options.ExchangeName = rabbitMqSection[nameof(RabbitMqOptions.ExchangeName)] ?? options.ExchangeName;
            options.QueueName = rabbitMqSection[nameof(RabbitMqOptions.QueueName)] ?? options.QueueName;
            options.RetryQueueName = rabbitMqSection[nameof(RabbitMqOptions.RetryQueueName)] ?? options.RetryQueueName;
            options.DeadLetterQueueName = rabbitMqSection[nameof(RabbitMqOptions.DeadLetterQueueName)] ?? options.DeadLetterQueueName;
            if (int.TryParse(rabbitMqSection[nameof(RabbitMqOptions.RetryDelayMilliseconds)], out var retryDelay))
            {
                options.RetryDelayMilliseconds = retryDelay;
            }

            if (int.TryParse(rabbitMqSection[nameof(RabbitMqOptions.MaxRetryCount)], out var maxRetryCount))
            {
                options.MaxRetryCount = maxRetryCount;
            }
        });
        services.AddHostedService<RabbitMqConsumerService>();
        return services;
    }
}
