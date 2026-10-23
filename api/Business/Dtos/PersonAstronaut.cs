namespace StargateAPI.Business.Dtos
{
    public class PersonAstronaut
    {
        public int PersonId { get; set; }

        public string Name { get; set; } = string.Empty;

        // Null for someone with no astronaut record, like the dates below. Dapper leaves a property at its default when
        // the column is NULL, so a string.Empty default here came back as "".
        public string? CurrentRank { get; set; }

        public string? CurrentDutyTitle { get; set; }

        public DateTime? CareerStartDate { get; set; }

        public DateTime? CareerEndDate { get; set; }


    }
}
