using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GadgetVault.Migrations
{
    /// <inheritdoc />
    public partial class CleanupTrackingNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE SalesOrders SET TrackingNumber = 'GV-' + FORMAT(OrderDate, 'yyyyMMdd') + '-' + RIGHT(CAST(ABS(CHECKSUM(NEWID())) AS VARCHAR(10)), 4) WHERE (TrackingNumber IS NULL OR TrackingNumber = '' OR TrackingNumber = 'N/A') AND Status = 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
