using System;
using Xunit;
using ElecKit.Core;

namespace ElecKit.Tests
{
    public class CsvLineParserTests
    {
        [Fact]
        public void ParsesSimpleAndEmptyCells()
        {
            Assert.Equal(new[] { "ElementId", "Mark", "", "Comments" },
                CsvLineParser.Parse("ElementId,Mark,,Comments"));
        }

        [Fact]
        public void ParsesCommasInsideQuotedCells()
        {
            Assert.Equal(new[] { "42", "Panel, Level 1", "OK" },
                CsvLineParser.Parse("42,\"Panel, Level 1\",OK"));
        }

        [Fact]
        public void UnescapesDoubledQuotes()
        {
            Assert.Equal(new[] { "A \"quoted\" value" },
                CsvLineParser.Parse("\"A \"\"quoted\"\" value\""));
        }

        [Fact]
        public void PreservesTrailingEmptyCell()
        {
            Assert.Equal(new[] { "a", "b", "" }, CsvLineParser.Parse("a,b,"));
        }

        [Fact]
        public void EscapingAndParsingRoundTripSpecialCharacters()
        {
            string original = "Panel, \"A\"\r\nLevel";
            Assert.Equal(original, CsvLineParser.Parse(Csv.Escape(original))[0]);
        }

        [Fact]
        public void RejectsUnterminatedQuotedField()
        {
            Assert.Throws<FormatException>(() => CsvLineParser.Parse("1,\"unfinished"));
        }

        [Fact]
        public void RejectsNullInput()
        {
            Assert.Throws<ArgumentNullException>(() => CsvLineParser.Parse(null));
        }
    }
}