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
    }
}
