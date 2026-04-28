using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GadgetVault.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserAndPOStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SupplierId",
                table: "Users",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_BusinessPartners_SupplierId",
                table: "Users",
                column: "SupplierId",
                principalTable: "BusinessPartners",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_BusinessPartners_SupplierId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SupplierId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Users");
        }
    }
}
