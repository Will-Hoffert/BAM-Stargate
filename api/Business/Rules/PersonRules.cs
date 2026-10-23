namespace StargateAPI.Business.Rules
{
    public static class PersonRules
    {
        public const int MaxNameLength = 100;

        // Surrounding spaces never make a different person. Spacing inside the name is kept as sent.
        public static string NormalizeName(string? name)
        {
            return name?.Trim() ?? string.Empty;
        }

        // For a name that identifies an existing person: returns the normalized name, or rejects a blank one.
        public static string RequireName(string? name)
        {
            var normalized = NormalizeName(name);

            if (normalized.Length == 0) throw new BadHttpRequestException("A person's name is required.", StatusCodes.Status400BadRequest);

            return normalized;
        }

        // For a name being stored: also rejects one that is too long.
        public static string ValidateName(string? name)
        {
            var normalized = RequireName(name);

            if (normalized.Length > MaxNameLength) throw new BadHttpRequestException($"A person's name can't be longer than {MaxNameLength} characters.", StatusCodes.Status400BadRequest);

            return normalized;
        }
    }
}
