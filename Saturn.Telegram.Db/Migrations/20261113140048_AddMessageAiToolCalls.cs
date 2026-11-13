using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saturn.Telegram.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageAiToolCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_tool_calls",
                table: "messages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_tool_calls",
                table: "messages");
        }
    }
}
