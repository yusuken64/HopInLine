using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HopInLine.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AutoAdvanceLine = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoAdvanceInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    CountDownStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPaused = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoReAdd = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoRestartTimerOnAdvance = table.Column<bool>(type: "INTEGER", nullable: false),
                    UnpauseRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    GraceTime = table.Column<int>(type: "INTEGER", nullable: false),
                    StartCountdown = table.Column<int>(type: "INTEGER", nullable: false),
                    DelayEndsAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DelayIsStartCountdown = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextAdvanceAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ManageToken = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    NextPosition = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    LeaveToken = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TurnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Removed = table.Column<bool>(type: "INTEGER", nullable: false),
                    LineId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Participants_Lines_LineId",
                        column: x => x.LineId,
                        principalTable: "Lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lines_NextAdvanceAt",
                table: "Lines",
                column: "NextAdvanceAt");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_LineId",
                table: "Participants",
                column: "LineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "Lines");
        }
    }
}
