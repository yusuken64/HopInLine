using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HopInLine.Migrations
{
    /// <inheritdoc />
    public partial class AddManageTokenAndAdvanceDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Participants",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Participants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Participants",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // Convert AutoAdvanceInterval from time to int (seconds)
            migrationBuilder.AddColumn<int>(
                name: "AutoAdvanceInterval_temp",
                table: "Lines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE Lines SET AutoAdvanceInterval_temp = DATEDIFF(SECOND, '00:00:00', AutoAdvanceInterval)");

            migrationBuilder.DropColumn(
                name: "AutoAdvanceInterval",
                table: "Lines");

            migrationBuilder.RenameColumn(
                name: "AutoAdvanceInterval_temp",
                table: "Lines",
                newName: "AutoAdvanceInterval");

            // Convert UnpauseRemaining from time to int (seconds)
            migrationBuilder.AddColumn<int>(
                name: "UnpauseRemaining_temp",
                table: "Lines",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Lines SET UnpauseRemaining_temp = DATEDIFF(SECOND, '00:00:00', UnpauseRemaining) WHERE UnpauseRemaining IS NOT NULL");

            migrationBuilder.DropColumn(
                name: "UnpauseRemaining",
                table: "Lines");

            migrationBuilder.RenameColumn(
                name: "UnpauseRemaining_temp",
                table: "Lines",
                newName: "UnpauseRemaining");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Lines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Lines",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManageToken",
                table: "Lines",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAdvanceAt",
                table: "Lines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Lines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "AutoRestartTimerOnAdvance",
                table: "Lines",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Lines_NextAdvanceAt",
                table: "Lines",
                column: "NextAdvanceAt");

            // Repair steps
            // a. Backfill ManageToken for existing rows
            migrationBuilder.Sql(
                "UPDATE Lines SET ManageToken = LOWER(REPLACE(CONVERT(nvarchar(36), NEWID()), '-', '')) WHERE ManageToken = '' OR ManageToken IS NULL");

            // b. Backfill Version
            migrationBuilder.Sql(
                "UPDATE Lines SET Version = NEWID()");

            // c. Renumber active participants per line to a dense 1..N
            migrationBuilder.Sql(
                @"WITH ordered AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY LineId ORDER BY Position, Id) AS rn
                    FROM Participants WHERE Removed = 0
                )
                UPDATE p SET p.Position = o.rn FROM Participants p JOIN ordered o ON p.Id = o.Id");

            // d. Set NextPosition past the high-water mark for every line
            migrationBuilder.Sql(
                @"UPDATE l SET l.NextPosition = ISNULL((SELECT MAX(p.Position) FROM Participants p WHERE p.LineId = l.Id), 0) + 1
                FROM Lines l");

            // e. Backfill NextAdvanceAt for lines whose timer should be live
            migrationBuilder.Sql(
                "UPDATE Lines SET NextAdvanceAt = DATEADD(SECOND, AutoAdvanceInterval, CountDownStart) WHERE AutoAdvanceLine = 1 AND IsPaused = 0");

            // f. Fix AutoRestartTimerOnAdvance default for existing rows
            migrationBuilder.Sql(
                "UPDATE Lines SET AutoRestartTimerOnAdvance = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lines_NextAdvanceAt",
                table: "Lines");

            migrationBuilder.DropColumn(
                name: "ManageToken",
                table: "Lines");

            migrationBuilder.DropColumn(
                name: "NextAdvanceAt",
                table: "Lines");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Lines");

            // Reverse AutoRestartTimerOnAdvance default change
            migrationBuilder.AlterColumn<bool>(
                name: "AutoRestartTimerOnAdvance",
                table: "Lines",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            // Convert AutoAdvanceInterval from int back to time
            migrationBuilder.AddColumn<TimeSpan>(
                name: "AutoAdvanceInterval_temp",
                table: "Lines",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0));

            migrationBuilder.Sql(
                "UPDATE Lines SET AutoAdvanceInterval_temp = CONVERT(time, DATEADD(SECOND, AutoAdvanceInterval, '00:00:00'))");

            migrationBuilder.DropColumn(
                name: "AutoAdvanceInterval",
                table: "Lines");

            migrationBuilder.RenameColumn(
                name: "AutoAdvanceInterval_temp",
                table: "Lines",
                newName: "AutoAdvanceInterval");

            // Convert UnpauseRemaining from int back to time
            migrationBuilder.AddColumn<TimeSpan>(
                name: "UnpauseRemaining_temp",
                table: "Lines",
                type: "time",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Lines SET UnpauseRemaining_temp = CONVERT(time, DATEADD(SECOND, UnpauseRemaining, '00:00:00')) WHERE UnpauseRemaining IS NOT NULL");

            migrationBuilder.DropColumn(
                name: "UnpauseRemaining",
                table: "Lines");

            migrationBuilder.RenameColumn(
                name: "UnpauseRemaining_temp",
                table: "Lines",
                newName: "UnpauseRemaining");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Participants",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Participants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Participants",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Lines",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Lines",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
