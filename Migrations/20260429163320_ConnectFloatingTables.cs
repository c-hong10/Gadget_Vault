using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GadgetVault.Migrations
{
    /// <inheritdoc />
    public partial class ConnectFloatingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockLevels_WarehouseLocations_LocationId",
                table: "StockLevels");

            migrationBuilder.AddColumn<int>(
                name: "UpdatedById",
                table: "SystemSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "SystemSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "LocationId",
                table: "StockLevels",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_UpdatedById",
                table: "SystemSettings",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_StockLevels_WarehouseLocations_LocationId",
                table: "StockLevels",
                column: "LocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SystemSettings_Users_UpdatedById",
                table: "SystemSettings",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockLevels_WarehouseLocations_LocationId",
                table: "StockLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemSettings_Users_UpdatedById",
                table: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_SystemSettings_UpdatedById",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "SystemSettings");

            migrationBuilder.AlterColumn<int>(
                name: "LocationId",
                table: "StockLevels",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockLevels_WarehouseLocations_LocationId",
                table: "StockLevels",
                column: "LocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
