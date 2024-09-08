using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HopInLine.Migrations
{
    public partial class UpdateLineAndParticipantModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Participants_Lines_LineId",
                table: "Participants");

            migrationBuilder.RenameColumn(
                name: "AudoReAdd",
                table: "Lines",
                newName: "AutoReAdd");

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "Participants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Removed",
                table: "Participants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_Participants_Lines_LineId",
                table: "Participants",
                column: "LineId",
                principalTable: "Lines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Participants_Lines_LineId",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "Removed",
                table: "Participants");

            migrationBuilder.RenameColumn(
                name: "AutoReAdd",
                table: "Lines",
                newName: "AudoReAdd");

            migrationBuilder.AddForeignKey(
                name: "FK_Participants_Lines_LineId",
                table: "Participants",
                column: "LineId",
                principalTable: "Lines",
                principalColumn: "Id");
        }
    }
}
