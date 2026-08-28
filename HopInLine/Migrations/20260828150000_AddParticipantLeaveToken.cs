using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HopInLine.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantLeaveToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LeaveToken",
                table: "Participants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Participants SET LeaveToken = LOWER(REPLACE(CONVERT(nvarchar(36), NEWID()), '-', '')) WHERE LeaveToken = '' OR LeaveToken IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaveToken",
                table: "Participants");
        }
    }
}
