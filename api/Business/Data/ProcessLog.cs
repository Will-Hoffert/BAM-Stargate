using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace StargateAPI.Business.Data
{
    // One row per request, recording how it ended.
    [Table("ProcessLog")]
    public class ProcessLog
    {
        public int Id { get; set; }

        public DateTime TimestampUtc { get; set; }

        public ProcessLogLevel Level { get; set; }

        // The request's type name, such as CreateAstronautDuty.
        public string Operation { get; set; } = string.Empty;

        // What the caller was told. For an unexpected error that is the generic message, never the exception's own.
        public string Message { get; set; } = string.Empty;

        public string RequestJson { get; set; } = string.Empty;

        // What was actually thrown, for whoever operates the system. Null when the request succeeded.
        public string? Exception { get; set; }

        public long DurationMs { get; set; }
    }

    public enum ProcessLogLevel
    {
        Information,
        Warning,
        Error
    }

    public class ProcessLogConfiguration : IEntityTypeConfiguration<ProcessLog>
    {
        public void Configure(EntityTypeBuilder<ProcessLog> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            // Stored as its name, so the table reads as words in SQLite Viewer and in plain SQL.
            builder.Property(x => x.Level).HasConversion<string>();
        }
    }
}
