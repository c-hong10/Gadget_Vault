using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GadgetVault.Migrations
{
    /// <inheritdoc />
    public partial class LinkStockToLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "StockLevels",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_StockLevels_LocationId",
                table: "StockLevels",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockLevels_WarehouseLocations_LocationId",
                table: "StockLevels",
                column: "LocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockLevels_WarehouseLocations_LocationId",
                table: "StockLevels");

            migrationBuilder.DropIndex(
                name: "IX_StockLevels_LocationId",
                table: "StockLevels");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "StockLevels");
        }
    }
}
