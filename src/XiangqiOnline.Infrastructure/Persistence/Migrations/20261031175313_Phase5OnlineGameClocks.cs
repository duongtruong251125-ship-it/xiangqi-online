using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XiangqiOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5OnlineGameClocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BlackRemainingMs",
                table: "GameSnapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 600000L);

            migrationBuilder.AddColumn<int>(
                name: "CurrentTurn",
                table: "GameSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "RedRemainingMs",
                table: "GameSnapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 600000L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "GameSnapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "BlackRemainingMs",
                table: "Games",
                type: "bigint",
                nullable: false,
                defaultValue: 600000L);

            migrationBuilder.AddColumn<long>(
                name: "RedRemainingMs",
                table: "Games",
                type: "bigint",
                nullable: false,
                defaultValue: 600000L);

            migrationBuilder.AddColumn<long>(
                name: "TimeControlIncrementMs",
                table: "Games",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TimeControlInitialMs",
                table: "Games",
                type: "bigint",
                nullable: false,
                defaultValue: 600000L);

            migrationBuilder.AddColumn<DateTime>(
                name: "TurnStartedAtUtc",
                table: "Games",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ClockAfterMove",
                table: "GameMoves",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FenBefore",
                table: "GameMoves",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlayerId",
                table: "GameMoves",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_Mode_Status_TurnStartedAtUtc",
                table: "Games",
                columns: new[] { "Mode", "Status", "TurnStartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GameMoves_PlayerId",
                table: "GameMoves",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_GameMoves_Users_PlayerId",
                table: "GameMoves",
                column: "PlayerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("UPDATE [Games] SET [TurnStartedAtUtc] = SYSUTCDATETIME() WHERE [Mode] = 0 AND [Status] = 1 AND [TurnStartedAtUtc] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameMoves_Users_PlayerId",
                table: "GameMoves");

            migrationBuilder.DropIndex(
                name: "IX_Games_Mode_Status_TurnStartedAtUtc",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_GameMoves_PlayerId",
                table: "GameMoves");

            migrationBuilder.DropColumn(
                name: "BlackRemainingMs",
                table: "GameSnapshots");

            migrationBuilder.DropColumn(
                name: "CurrentTurn",
                table: "GameSnapshots");

            migrationBuilder.DropColumn(
                name: "RedRemainingMs",
                table: "GameSnapshots");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "GameSnapshots");

            migrationBuilder.DropColumn(
                name: "BlackRemainingMs",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "RedRemainingMs",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "TimeControlIncrementMs",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "TimeControlInitialMs",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "TurnStartedAtUtc",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ClockAfterMove",
                table: "GameMoves");

            migrationBuilder.DropColumn(
                name: "FenBefore",
                table: "GameMoves");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "GameMoves");
        }
    }
}
