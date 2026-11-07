using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PnlCapture.Domain.Entities;

namespace PnlCapture.Infrastructure.Persistence;

public sealed class PnlDbContext(DbContextOptions<PnlDbContext> options) : DbContext(options)
{
    public DbSet<PnlRecord> PnlRecords => Set<PnlRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var record = modelBuilder.Entity<PnlRecord>();
        record.ToTable("PnlRecords");
        record.HasKey(item => item.Id);
        record.Ignore(item => item.ValidationStatus);

        record.Property(item => item.SourceSystem).IsRequired();
        record.Property(item => item.AccountNumber).IsRequired();
        record.Property(item => item.PnlAmount)
            .HasConversion(
                amount => amount.ToString(CultureInfo.InvariantCulture),
                value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture))
            .HasColumnType("TEXT")
            .IsRequired();
        record.Property(item => item.IsValid)
            .HasField("_isValid")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();
        record.Property(item => item.FeedType)
            .HasConversion<string>()
            .IsRequired();
        record.Property(item => item.ProcessedAtUtc)
            .HasConversion(
                value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
            .HasColumnType("TEXT")
            .IsRequired();

        record.HasIndex(item => item.MessageId)
            .IsUnique()
            .HasFilter("\"MessageId\" IS NOT NULL");
        record.HasIndex(item => new { item.IsValid, item.ProcessedAtUtc });
        record.HasIndex(item => new { item.SourceSystem, item.FeedType, item.ProcessedAtUtc });

        base.OnModelCreating(modelBuilder);
    }
}
