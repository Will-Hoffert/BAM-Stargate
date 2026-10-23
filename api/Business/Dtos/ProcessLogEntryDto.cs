namespace StargateAPI.Business.Dtos
{
    // A ProcessLog row as the API shows it. The Exception column is left out: it can hold internal details such as SQL.
    public class ProcessLogEntryDto
    {
        public int Id { get; set; }

        public DateTime TimestampUtc { get; set; }

        public string Level { get; set; } = string.Empty;

        public string Operation { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string RequestJson { get; set; } = string.Empty;

        public long DurationMs { get; set; }
    }
}
