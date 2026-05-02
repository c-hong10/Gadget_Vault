using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GadgetVault.Migrations
{
    /// <inheritdoc />
    public partial class FinalTrackingPolish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE SalesOrders SET TrackingNumber = 'GV-20260502-7782' WHERE SONumber = 'SO-20260502-001'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
