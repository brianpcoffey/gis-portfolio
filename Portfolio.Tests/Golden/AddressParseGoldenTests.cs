using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Golden
{
    // Golden output for the address parser before AddressStandardizationService.ParseAddress
    // is refactored. Every field of AddressParsedDto is pinned for about thirty raw inputs.
    //
    // The rows record what the parser does today, including its limits: "#4", "Hwy 99" and
    // "Floor 3" are not recognised as units or street names and fall into the neighbouring
    // fields. Those rows are here so a refactor cannot change them by accident; if a later
    // change fixes them on purpose, update the rows in the same commit.
    public class AddressParseGoldenTests
    {
        private static AddressStandardizationService NewService() =>
            new(new HttpClient(), new Mock<ILogger<AddressStandardizationService>>().Object);

        [Theory]
        [InlineData("123 Main St, Springfield, IL 62701", "123", "Main", "Street", "", "Springfield", "IL", "62701", "123 Main Street, Springfield, IL 62701", 1.0)]
        [InlineData("1600 Pennsylvania Ave NW, Washington, DC 20500", "1600", "Pennsylvania", "Avenue", "", "Washington", "DC", "20500", "1600 Pennsylvania Avenue NW, Washington, DC 20500", 1.0)]
        [InlineData("600 Broadway, Redlands", "600", "Broadway", "", "", "Redlands", "", "", "600 Broadway, Redlands", 0.5)]
        [InlineData("380 New York St, Redlands, CA 92373", "380", "New York", "Street", "", "Redlands", "CA", "92373", "380 New York Street, Redlands, CA 92373", 1.0)]
        [InlineData("42 Elm Street Apt 5B, Portland, OR 97201", "42", "Elm", "Street", "Apt 5B", "Portland", "OR", "97201", "42 Elm Street Apt 5B, Portland, OR 97201", 1.0)]
        [InlineData("7 Oak Dr Unit 12, Austin, TX 78701", "7", "Oak", "Drive", "Unit 12", "Austin", "TX", "78701", "7 Oak Drive Unit 12, Austin, TX 78701", 1.0)]
        [InlineData("1 Infinite Loop, Cupertino, CA 95014", "1", "Infinite", "Loop", "", "Cupertino", "CA", "95014", "1 Infinite Loop, Cupertino, CA 95014", 1.0)]
        [InlineData("500 W 5th Ave Suite 300, Anchorage, AK 99501", "500", "W 5th", "Avenue", "Suite 300", "Anchorage", "AK", "99501", "500 W 5th Avenue Suite 300, Anchorage, AK 99501", 1.0)]
        [InlineData("  221B   Baker   Street ,  Boston , MA  02108 ", "221B", "Baker", "Street", "", "Boston", "MA", "02108", "221B Baker Street, Boston, MA 02108", 1.0)]
        [InlineData("100 N Main Blvd, Los Angeles, CA", "100", "N Main", "Boulevard", "", "Los Angeles", "CA", "", "100 N Main Boulevard, Los Angeles, CA", 0.8333333333333334)]
        [InlineData("55 Ocean Pkwy, Brooklyn, NY 11218", "55", "Ocean", "Parkway", "", "Brooklyn", "NY", "11218", "55 Ocean Parkway, Brooklyn, NY 11218", 1.0)]
        [InlineData("9 Maple Ln, Denver, CO 80202-1234", "9", "Maple", "Lane", "", "Denver", "CO", "80202-1234", "9 Maple Lane, Denver, CO 80202-1234", 1.0)]
        [InlineData("123A Pine Rd, Seattle, WA 98101", "123A", "Pine", "Road", "", "Seattle", "WA", "98101", "123A Pine Road, Seattle, WA 98101", 1.0)]
        [InlineData("742 Evergreen Terrace, Springfield, OR 97477", "742", "Evergreen", "Terrace", "", "Springfield", "OR", "97477", "742 Evergreen Terrace, Springfield, OR 97477", 1.0)]
        [InlineData("12 Sunset Blvd #4, Los Angeles, CA 90028", "12", "Sunset", "Boulevard", "", "#4 Los Angeles", "CA", "90028", "12 Sunset Boulevard, #4 Los Angeles, CA 90028", 1.0)]
        [InlineData("88 Lakeview Ct, Chicago, IL 60601", "88", "Lakeview", "Court", "", "Chicago", "IL", "60601", "88 Lakeview Court, Chicago, IL 60601", 1.0)]
        [InlineData("350 Fifth Avenue, New York, NY 10118", "350", "Fifth", "Avenue", "", "New York", "NY", "10118", "350 Fifth Avenue, New York, NY 10118", 1.0)]
        [InlineData("1 Main St", "1", "Main", "Street", "", "", "", "", "1 Main Street", 0.5)]
        [InlineData("Main St", "", "Main", "Street", "", "", "", "", "Main Street", 0.3333333333333333)]
        [InlineData("Redlands CA 92373", "", "Redlands", "", "", "", "CA", "92373", "Redlands, CA 92373", 0.5)]
        [InlineData("92373", "", "", "", "", "", "", "92373", ", 92373", 0.16666666666666666)]
        [InlineData("10 Downing Way NE, Salem, OR 97301", "10", "Downing", "Way", "", "Salem", "OR", "97301", "10 Downing Way NE, Salem, OR 97301", 1.0)]
        [InlineData("2000 E Colorado Blvd, Pasadena, CA 91107", "2000", "E Colorado", "Boulevard", "", "Pasadena", "CA", "91107", "2000 E Colorado Boulevard, Pasadena, CA 91107", 1.0)]
        [InlineData("17 Walnut Cir, San Bernardino, CA 92401", "17", "Walnut", "Circle", "", "San Bernardino", "CA", "92401", "17 Walnut Circle, San Bernardino, CA 92401", 1.0)]
        [InlineData("4 Privet Dr, Little Whinging, NE 68001", "4", "Privet", "Drive", "", "Little Whinging", "NE", "68001", "4 Privet Drive, Little Whinging, NE 68001", 1.0)]
        [InlineData("999 Hwy 99, Fresno, CA 93721", "999", "", "Highway", "", "99 Fresno", "CA", "93721", "999 Highway, 99 Fresno, CA 93721", 0.8333333333333334)]
        [InlineData("31 Park Pl Ste 2, Boston, MA 02110", "31", "Park", "Place", "Ste 2", "Boston", "MA", "02110", "31 Park Place Ste 2, Boston, MA 02110", 1.0)]
        [InlineData("2525 Rd, Reno, NV 89501", "2525", "", "Road", "", "Reno", "NV", "89501", "2525 Road, Reno, NV 89501", 0.8333333333333334)]
        [InlineData("8 Cedar Aly, Madison, WI 53703", "8", "Cedar", "Alley", "", "Madison", "WI", "53703", "8 Cedar Alley, Madison, WI 53703", 1.0)]
        [InlineData("1234 Mission Road Floor 3, San Francisco, CA 94110", "1234", "Mission", "Road", "", "Floor 3 San Francisco", "CA", "94110", "1234 Mission Road, Floor 3 San Francisco, CA 94110", 1.0)]
        [InlineData("42 Elm Street Apt 5B, Portland, OR", "42", "Elm", "Street", "Apt 5B", "Portland", "OR", "", "42 Elm Street Apt 5B, Portland, OR", 0.8571428571428571)]
        public async Task Parse_PinsEveryField(
            string raw,
            string houseNumber,
            string streetName,
            string streetSuffix,
            string unit,
            string city,
            string state,
            string postalCode,
            string standardized,
            double confidence)
        {
            var parsed = await NewService().ParseAsync(raw);

            Assert.Equal(houseNumber, parsed.HouseNumber);
            Assert.Equal(streetName, parsed.StreetName);
            Assert.Equal(streetSuffix, parsed.StreetSuffix);
            Assert.Equal(unit, parsed.Unit);
            Assert.Equal(city, parsed.City);
            Assert.Equal(state, parsed.State);
            Assert.Equal(postalCode, parsed.PostalCode);
            Assert.Equal(standardized, parsed.StandardizedAddress);
            Assert.Equal(confidence, parsed.ParseConfidence, 1e-9);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Parse_BlankInput_Throws(string raw)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => NewService().ParseAsync(raw));
        }

        [Fact]
        public async Task Parse_NullInput_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => NewService().ParseAsync(null!));
        }
    }
}
