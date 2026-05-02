using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GadgetVault.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingToSalesOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShippingLabelUrl",
                table: "SalesOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "SalesOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingLabelUrl",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "SalesOrders");
        }
    }
}
