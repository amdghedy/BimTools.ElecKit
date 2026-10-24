using System;
using Xunit;
using ElecKit.Core;

namespace ElecKit.Tests
{
    public class SmartFilterLogicTests
    {
        [Fact]
        public void AndMatchesOnlyWhenEveryRuleMatches()
        {
            Assert.True(SmartFilterLogic.Matches(new[] { true, true, true }, true));
            Assert.False(SmartFilterLogic.Matches(new[] { true, false, true }, true));
            Assert.False(SmartFilterLogic.Matches(new[] { false, false }, true));
        }

        [Fact]
        public void OrMatchesWhenAnyRuleMatches()
        {
            Assert.True(SmartFilterLogic.Matches(new[] { false, true, false }, false));
            Assert.True(SmartFilterLogic.Matches(new[] { true, false }, false));
            Assert.False(SmartFilterLogic.Matches(new[] { false, false, false }, false));
        }

        [Fact]
        public void SingleRuleHasSameResultForAndAndOr()
        {
            Assert.True(SmartFilterLogic.Matches(new[] { true }, true));
            Assert.True(SmartFilterLogic.Matches(new[] { true }, false));
            Assert.False(SmartFilterLogic.Matches(new[] { false }, true));
            Assert.False(SmartFilterLogic.Matches(new[] { false }, false));
        }

        [Fact]
        public void EmptyRulesNeverMatch()
        {
            Assert.False(SmartFilterLogic.Matches(Array.Empty<bool>(), true));
            Assert.False(SmartFilterLogic.Matches(Array.Empty<bool>(), false));
        }

        [Fact]
        public void NullRuleResultsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => SmartFilterLogic.Matches(null, true));
        }

        [Theory]
        [InlineData("Panel A", "panel a", true)]
        [InlineData("ABC-01", "ABC-01", true)]
        [InlineData("ABC-01", "ABC-02", false)]
        [InlineData("", "", true)]
        [InlineData(null, "", true)]
        [InlineData(" Value ", "Value", false)]
        public void ComparesDisplayedValuesCaseInsensitivelyWithoutTrimming(string actual, string expected, bool match)
        {
            Assert.Equal(match, SmartFilterLogic.ValuesEqual(actual, expected));
        }
    }
}