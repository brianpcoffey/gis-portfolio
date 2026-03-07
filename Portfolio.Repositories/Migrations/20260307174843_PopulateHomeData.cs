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
                DO
                $$
                DECLARE
                    i INT;
                    streets TEXT[] := ARRAY[
                        '123 Cajon St','456 Citrus Ave','789 Redlands Blvd','101 Orange St','202 Brookside Ave',
                        '303 Church St','404 Eureka St','505 Vine St','606 Highland Ave','707 Palm Ave',
                        '808 Lugonia Ave','909 Pioneer Ave','111 Center St','222 Fifth Ave','333 Colton Ave',
                        '444 State St','555 Ford St','666 San Mateo St','777 Terracina Blvd','888 Alabama St',
                        '999 Iowa St','110 Nevada St','220 Arizona St','330 Judson St','440 Grant St',
                        '550 Fern Ave','660 Garden St','770 Occidental Dr','880 Sunset Dr','990 Panorama Dr',
                        '100 San Bernardino Ave','200 New York St','300 Sixth St','400 Webster St','500 Hinckley St',
                        '600 Lawton St','700 Alvarado St','800 Crescent Ave','900 Holmes St','121 Barton Rd',
                        '131 Dearborn St','141 Clay St','151 E Cypress Ave','161 W Olive Ave','171 Alta Vista Dr',
                        '181 W Fern Ave','191 E Colton Ave','201 Mariposa Dr','211 Stillman Ave','221 Serpentine Dr'
                    ];
                    street_count INT := array_length(streets,1);
                BEGIN
                    FOR i IN 1..250 LOOP
                        INSERT INTO ""Properties""(
                            ""BrokeredBy"",""Status"",""Price"",""Bedrooms"",""Bathrooms"",""AcreLot"",""LotSqft"",""Street"",
                            ""City"",""State"",""ZipCode"",""Latitude"",""Longitude"",""HoaFee"",""PropertyTax"",""Utilities"",
                            ""SchoolRating"",""CrimeScore"",""Walkability"",""TransitAccess"",""AmenitiesScore"",
                            ""CommuteMin"",""YearBuilt"",""LastRenovation"",""RoofCondition"",""AcCondition"",""PlumbingCondition"",
                            ""ElectricalCondition"",""FloorPlanScore"",""FutureAppreciation"",""ResalePotential"",""FloodRisk"",""NoiseLevel""
                        )
                        VALUES (
                            'Broker ' || (i % 20 + 1),
                            'active',
                            (300000 + floor(random()*1200000))::numeric(12,2),
                            (2 + floor(random()*4))::int,
                            (1 + floor(random()*3))::int,
                            round((0.1 + random()*0.8)::numeric,2),
                            (1000 + floor(random()*4000))::int,
                            streets[1 + floor(random()*street_count)],
                            'Redlands',
                            'CA',
                            (92373 + floor(random()*3))::text,
                            34.0556 + (random()-0.5)*0.04,
                            -117.1825 + (random()-0.5)*0.06,
                            (floor(random()*400))::numeric(10,2),
                            (2000 + floor(random()*10000))::numeric(10,2),
                            (100 + floor(random()*300))::numeric(10,2),
                            (30 + floor(random()*70))::int,
                            (5 + floor(random()*75))::int,
                            (20 + floor(random()*75))::int,
                            (10 + floor(random()*75))::int,
                            (20 + floor(random()*75))::int,
                            (5 + floor(random()*55))::int,
                            (1950 + floor(random()*75))::int,
                            CASE WHEN random()<0.5 THEN NULL ELSE (2005 + floor(random()*20))::int END,
                            (30 + floor(random()*70))::int,
                            (30 + floor(random()*70))::int,
                            (30 + floor(random()*70))::int,
                            (30 + floor(random()*70))::int,
                            (30 + floor(random()*70))::int,
                            (20 + floor(random()*70))::int,
                            (20 + floor(random()*70))::int,
                            floor(random()*50)::int,
                            (5 + floor(random()*65))::int
                        );
                    END LOOP;
                END
                $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""Properties"" WHERE ""City""='Redlands';");
        }
    }
}