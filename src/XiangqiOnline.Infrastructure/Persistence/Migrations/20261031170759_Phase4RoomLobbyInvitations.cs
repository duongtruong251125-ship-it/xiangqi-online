using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XiangqiOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4RoomLobbyInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invitations_InviteeId",
                table: "Invitations");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Rooms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Rooms",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Phòng mới");

            migrationBuilder.Sql("UPDATE [Rooms] SET [Name] = N'Phòng ' + [Code] WHERE [Name] = N'Phòng mới'");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Rooms",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IsReady",
                table: "RoomMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "Invitations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InviteeId_Status_ExpiresAt",
                table: "Invitations",
                columns: new[] { "InviteeId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RoomId_InviteeId",
                table: "Invitations",
                columns: new[] { "RoomId", "InviteeId" },
                unique: true,
                filter: "[InviteeId] IS NOT NULL AND [Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invitations_InviteeId_Status_ExpiresAt",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_RoomId_InviteeId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "IsReady",
                table: "RoomMembers");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "Invitations");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InviteeId",
                table: "Invitations",
                column: "InviteeId");
        }
    }
}
