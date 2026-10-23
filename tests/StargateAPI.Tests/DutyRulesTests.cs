using Microsoft.AspNetCore.Http;
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

        [Theory]
        [InlineData("  CPT  ", "CPT")]
        [InlineData("1LT", "1LT")]
        public void ValidateRank_Rank_ReturnsTrimmedRank(string rank, string expected)
        {
            Assert.Equal(expected, DutyRules.ValidateRank(rank));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateDutyTitle_BlankTitle_ThrowsBadRequest(string? dutyTitle)
        {
            var exception = Assert.Throws<BadHttpRequestException>(() => DutyRules.ValidateDutyTitle(dutyTitle));

            Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        }

        [Fact]
        public void ValidateDutyTitle_MaximumLength_IsAccepted()
        {
            var dutyTitle = new string('x', DutyRules.MaxFieldLength);

            Assert.Equal(dutyTitle, DutyRules.ValidateDutyTitle(dutyTitle));
        }

        [Fact]
        public void ValidateStartDate_TimeOfDay_ReturnsDateOnly()
        {
            Assert.Equal(new DateTime(2020, 1, 1), DutyRules.ValidateStartDate(new DateTime(2020, 1, 1, 14, 30, 0)));
        }

        [Fact]
        public void ValidateStartDate_MissingDate_ThrowsBadRequest()
        {
            var exception = Assert.Throws<BadHttpRequestException>(() => DutyRules.ValidateStartDate(default));

            Assert.Equal("A duty start date is required.", exception.Message);
        }
    }
}
