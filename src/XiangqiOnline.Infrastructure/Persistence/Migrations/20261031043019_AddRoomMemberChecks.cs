using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XiangqiOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomMemberChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_RoomMembers_Role",
                table: "RoomMembers",
                sql: "[Role] IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoomMembers_Side",
                table: "RoomMembers",
                sql: "[Side] IS NULL OR [Side] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RoomMembers_Role",
                table: "RoomMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoomMembers_Side",
                table: "RoomMembers");
        }
    }
}
