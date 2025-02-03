using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SimplePostgreSQLDataStore.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Flights",
                columns: table => new
                {
                    FlightIdentification = table.Column<string>(type: "text", nullable: false),
                    ScheduledTimeOfDeparture = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledTimeOfArrival = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRecalculating = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flights", x => x.FlightIdentification);
                });

            migrationBuilder.CreateTable(
                name: "Airports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FlightEntityId = table.Column<string>(type: "text", nullable: false),
                    ICAO = table.Column<string>(type: "text", nullable: false),
                    LastSeenWeatherCategory = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Airports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Airports_Flights_FlightEntityId",
                        column: x => x.FlightEntityId,
                        principalTable: "Flights",
                        principalColumn: "FlightIdentification",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Airports_FlightEntityId",
                table: "Airports",
                column: "FlightEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Airports");

            migrationBuilder.DropTable(
                name: "Flights");
        }
    }
}
