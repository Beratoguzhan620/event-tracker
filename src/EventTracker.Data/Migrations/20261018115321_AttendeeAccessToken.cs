using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttendeeAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Attendees",
                type: "TEXT",
                fixedLength: true,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Attendees",
                type: "TEXT",
                nullable: true);

            // Registrations made before this migration get their own random key, so the unique index holds.
            migrationBuilder.Sql("UPDATE \"Attendees\" SET \"AccessToken\" = lower(hex(randomblob(16))) WHERE \"AccessToken\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Attendees_AccessToken",
                table: "Attendees",
                column: "AccessToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendees_AccessToken",
                table: "Attendees");

            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "Attendees");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Attendees");
        }
    }
}
