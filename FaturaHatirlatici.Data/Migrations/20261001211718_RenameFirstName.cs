using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaturaHatirlatici.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameFirstName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Firstname",
                table: "BotUsers",
                newName: "FirstName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FirstName",
                table: "BotUsers",
                newName: "Firstname");
        }
    }
}
