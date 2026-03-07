using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class PopulateHomeData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""Properties""(
                ""BrokeredBy"",""Status"",""Price"",""Bedrooms"",""Bathrooms"",""AcreLot"",""LotSqft"",
                ""Street"",""City"",""State"",""ZipCode"",
                ""Latitude"",""Longitude"",
                ""HoaFee"",""PropertyTax"",""Utilities"",
                ""SchoolRating"",""CrimeScore"",""Walkability"",""TransitAccess"",""AmenitiesScore"",
                ""CommuteMin"",
                ""YearBuilt"",""LastRenovation"",
                ""RoofCondition"",""AcCondition"",""PlumbingCondition"",""ElectricalCondition"",
                ""FloorPlanScore"",
                ""FutureAppreciation"",""ResalePotential"",
                ""FloodRisk"",""NoiseLevel""
                )
                VALUES

                ('BHHS PERRIE MUNDY REALTY GROUP','active',995000,4,3,0.25,2131,'120 Franklin Ave','Redlands','CA','92373',34.055,-117.182,
                0,9950,300,
                80,60,55,40,65,
                25,
                1985,NULL,
                80,80,75,75,
                78,
                70,72,
                10,30),

                ('CROWN REAL ESTATE TEAM','active',439000,4,2,0.18,1504,'915 Alta St','Redlands','CA','92374',34.060,-117.170,
                0,4390,250,
                65,58,60,35,55,
                28,
                1978,NULL,
                70,72,70,68,
                65,
                60,62,
                12,35),

                ('COLDWELL BANKER REALTY','active',1395000,3,3,0.35,2776,'543 E Mariposa Dr','Redlands','CA','92373',34.052,-117.188,
                0,13950,350,
                88,70,52,30,75,
                24,
                1992,2018,
                85,85,83,82,
                88,
                80,85,
                8,20),

                ('KELLER WILLIAMS REALTY','active',999900,4,3,0.28,2310,'1582 Franklin Ave','Redlands','CA','92373',34.056,-117.181,
                0,9990,320,
                82,64,58,40,68,
                26,
                1987,2016,
                82,84,78,79,
                80,
                75,76,
                9,25),

                ('KELLER WILLIAMS REALTY','active',850000,4,3,0.30,2138,'31397 Mesa Dr','Redlands','CA','92373',34.050,-117.195,
                0,8500,300,
                78,60,45,25,60,
                30,
                1984,NULL,
                75,76,74,72,
                73,
                68,70,
                11,28),

                ('CENTURY 21 LOIS LAUER REALTY','active',650000,3,2,0.40,1795,'947 Nottingham Dr','Redlands','CA','92373',34.058,-117.187,
                0,6500,280,
                75,55,52,32,60,
                27,
                1975,2009,
                72,70,69,68,
                71,
                65,66,
                12,30),

                ('EXP REALTY OF GREATER LOS ANGELES','active',610000,4,2,0.22,1308,'1539 Robyn St','Redlands','CA','92374',34.064,-117.166,
                0,6100,260,
                70,57,50,30,55,
                29,
                1980,NULL,
                70,72,71,70,
                68,
                63,64,
                13,34),

                ('CENTURY 21 LOIS LAUER REALTY','active',949000,3,3,0.27,2451,'171 Bellevue Ave','Redlands','CA','92373',34.057,-117.183,
                0,9490,310,
                83,60,60,38,72,
                24,
                1988,2015,
                80,82,79,78,
                82,
                72,74,
                10,27),

                ('KELLER WILLIAMS EMPIRE ESTATES','active',365000,3,3,0.05,1543,'135 E Cypress Ave','Redlands','CA','92373',34.053,-117.183,
                350,3650,250,
                68,62,70,55,65,
                20,
                1995,NULL,
                74,75,72,72,
                70,
                60,61,
                10,40),

                ('SHAW REAL ESTATE BROKERS','active',797000,3,3,0.26,2068,'162 Lakeside Ave','Redlands','CA','92373',34.055,-117.178,
                0,7970,300,
                80,58,55,35,68,
                26,
                1990,2017,
                82,84,82,80,
                83,
                72,74,
                9,25),

                ('FIRST TEAM REAL ESTATE','active',575000,3,2,0.20,1324,'1427 Laramie Ave','Redlands','CA','92374',34.061,-117.169,
                0,5750,260,
                70,56,52,32,58,
                28,
                1983,NULL,
                70,72,71,70,
                68,
                63,65,
                13,34),

                ('EXP REALTY OF CALIFORNIA INC.','active',870000,5,4,0.33,3091,'1678 Harrison Ln','Redlands','CA','92374',34.063,-117.168,
                0,8700,320,
                77,55,50,30,60,
                30,
                1994,2020,
                85,87,83,82,
                85,
                75,76,
                10,30),

                ('LPT REALTY INC','active',899900,5,3,0.30,3116,'1802 Pummelo Dr','Redlands','CA','92374',34.064,-117.167,
                0,8999,320,
                78,58,48,28,62,
                29,
                1996,NULL,
                82,83,81,80,
                82,
                72,74,
                11,30)
                ;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""Properties"" WHERE ""City""='Redlands';");
        }
    }
}