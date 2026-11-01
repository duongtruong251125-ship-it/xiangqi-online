using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XiangqiOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7HumanVsAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiCompletedDepth",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiDifficulty",
                table: "Games",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AiElapsedMs",
                table: "Games",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "AiLastError",
                table: "Games",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AiNodesSearched",
                table: "Games",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "AiRetryCount",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiSide",
                table: "Games",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AiTurnLeaseUntilUtc",
                table: "Games",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiTurnStatus",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "HumanPlayerId",
                table: "Games",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HumanSide",
                table: "Games",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerSide",
                table: "Games",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAiMove",
                table: "GameMoves",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Games_HumanPlayerId",
                table: "Games",
                column: "HumanPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Mode_AiTurnStatus_AiTurnLeaseUntilUtc",
                table: "Games",
                columns: new[] { "Mode", "AiTurnStatus", "AiTurnLeaseUntilUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_HumanPlayerId",
                table: "Games",
                column: "HumanPlayerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_HumanPlayerId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_HumanPlayerId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_Mode_AiTurnStatus_AiTurnLeaseUntilUtc",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AiCompletedDepth",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AiDifficulty",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AiElapsedMs",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AiLastError",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AiNodesSearched",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AiRetryCount",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AiSide",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AiTurnLeaseUntilUtc",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AiTurnStatus",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HumanPlayerId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HumanSide",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "WinnerSide",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsAiMove",
                table: "GameMoves");
        }
    }
}
