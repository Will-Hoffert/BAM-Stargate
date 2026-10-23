using StargateAPI.Business.Rules;

namespace StargateAPI.Tests
{
    public class DutyRulesTests
    {
        [Theory]
        [InlineData("RETIRED", true)]
        [InlineData("Retired", true)]
        [InlineData("retired", true)]
        [InlineData("Pilot", false)]
        [InlineData("RETIRED PILOT", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsRetirement_DutyTitle_MatchesRetiredInAnyCase(string? dutyTitle, bool expected)
        {
            Assert.Equal(expected, DutyRules.IsRetirement(dutyTitle));
        }
    }
}
