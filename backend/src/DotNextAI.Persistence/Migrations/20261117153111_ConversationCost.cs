using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNextAI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConversationCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "Messages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "Conversations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Conversations");
        }
    }
}
