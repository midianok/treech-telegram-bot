using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saturn.Telegram.Db.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAiAgentCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_agents_code",
                table: "ai_agents");

            migrationBuilder.DropColumn(
                name: "code",
                table: "ai_agents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "ai_agents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_ai_agents_code",
                table: "ai_agents",
                column: "code",
                unique: true);
        }
    }
}
