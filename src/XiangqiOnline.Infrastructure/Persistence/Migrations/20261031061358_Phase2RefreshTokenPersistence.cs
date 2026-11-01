using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XiangqiOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2RefreshTokenPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPersistent",
                table: "RefreshTokens",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPersistent",
                table: "RefreshTokens");
        }
    }
}
