using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace StargateAPI.Business.Data
{
    [Table("AstronautDuty")]
    public class AstronautDuty
    {
        public int Id { get; set; }

        public int PersonId { get; set; }

        public string Rank { get; set; } = string.Empty;

        public string DutyTitle { get; set; } = string.Empty;

        public DateTime DutyStartDate { get; set; }

        public DateTime? DutyEndDate { get; set; }

        // PersonId is required, so this is only null when EF hasn't loaded it.
        public virtual Person Person { get; set; } = null!;
    }

    public class AstronautDutyConfiguration : IEntityTypeConfiguration<AstronautDuty>
    {
        public void Configure(EntityTypeBuilder<AstronautDuty> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            // Declared so EF keeps it: the filtered index below only covers current duties, not a person's whole history.
            builder.HasIndex(x => x.PersonId);
            // Rules 3-4: a person has at most one current duty, the one without an end date.
            builder.HasIndex(x => x.PersonId, "IX_AstronautDuty_PersonId_CurrentDuty").IsUnique().HasFilter("[DutyEndDate] IS NULL");
        }
    }
}
