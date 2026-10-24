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

        [Fact]
        public void ParsesWholeDocumentWithNewlineInsideQuotedCell()
        {
            var rows = CsvLineParser.ParseRecords("ElementId,Comments\r\n42,\"First line\r\nSecond line\"\r\n43,OK\r\n");
            Assert.Equal(3, rows.Count);
            Assert.Equal(new[] { "ElementId", "Comments" }, rows[0]);
            Assert.Equal(new[] { "42", "First line\r\nSecond line" }, rows[1]);
            Assert.Equal(new[] { "43", "OK" }, rows[2]);
        }

        [Fact]
        public void ParsesWholeDocumentWithEscapedQuotesAndTrailingEmptyCell()
        {
            var rows = CsvLineParser.ParseRecords("Id,Value,\n1,\"A \"\"quoted\"\" value\",\n");
            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] { "1", "A \"quoted\" value", "" }, rows[1]);
        }

        [Fact]
        public void RejectsUnterminatedQuoteInWholeDocument()
        {
            Assert.Throws<FormatException>(() => CsvLineParser.ParseRecords("Id,Comments\n1,\"unfinished"));
        }
    }
}