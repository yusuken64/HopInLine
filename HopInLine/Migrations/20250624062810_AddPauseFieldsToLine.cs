using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HopInLine.Migrations
{
    /// <inheritdoc />
    public partial class AddPauseFieldsToLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoRestartTimerOnAdvance",
                table: "Lines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "Lines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "UnpauseRemaining",
                table: "Lines",
                type: "time",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoRestartTimerOnAdvance",
                table: "Lines");

            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "Lines");

            migrationBuilder.DropColumn(
                name: "UnpauseRemaining",
                table: "Lines");
        }
    }
}
