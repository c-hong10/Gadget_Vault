using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GadgetVault.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessPartnerIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "BusinessPartners",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "BusinessPartners");
        }
    }
}
