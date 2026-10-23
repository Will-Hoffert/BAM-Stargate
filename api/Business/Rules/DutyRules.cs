namespace StargateAPI.Business.Rules
{
    public static class DutyRules
    {
        // The duty title that retires a person (Rule 6). Retirements are stored with this exact spelling.
        public const string Retired = "RETIRED";

        // The feed may not match the README's capitalization, so any casing of RETIRED counts.
        public static bool IsRetirement(string? dutyTitle)
        {
            return string.Equals(dutyTitle, Retired, StringComparison.OrdinalIgnoreCase);
        }

        // Rank and duty title share one limit.
        public const int MaxFieldLength = 100;

        public static string ValidateRank(string? rank)
        {
            return ValidateField(rank, "rank");
        }

        public static string ValidateDutyTitle(string? dutyTitle)
        {
            return ValidateField(dutyTitle, "duty title");
        }

        // A missing date arrives as DateTime's default, 0001-01-01, which can never be a real duty date.
        // Start dates are stored without the time of day.
        public static DateTime ValidateStartDate(DateTime dutyStartDate)
        {
            if (dutyStartDate.Date == DateTime.MinValue) throw new BadHttpRequestException("A duty start date is required.", StatusCodes.Status400BadRequest);

            return dutyStartDate.Date;
        }

        // Returns the value without surrounding spaces, or rejects one that is blank or too long.
        private static string ValidateField(string? value, string fieldName)
        {
            var trimmed = value?.Trim() ?? string.Empty;

            if (trimmed.Length == 0) throw new BadHttpRequestException($"A {fieldName} is required.", StatusCodes.Status400BadRequest);

            if (trimmed.Length > MaxFieldLength) throw new BadHttpRequestException($"A {fieldName} can't be longer than {MaxFieldLength} characters.", StatusCodes.Status400BadRequest);

            return trimmed;
        }
    }
}
