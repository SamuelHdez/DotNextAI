using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNextAI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FolderAppearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Folders",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Folders",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "folder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Folders");
        }
    }
}
