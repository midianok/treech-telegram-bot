using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saturn.Telegram.Db.Migrations
{
    /// <inheritdoc />
    public partial class AiAgentEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "text",
                table: "messages",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "sticker_id",
                table: "messages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reply_to_message_chat_id",
                table: "messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reply_to_message_id",
                table: "messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ai_agent_id",
                table: "chats",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_agents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chats_ai_agent_id",
                table: "chats",
                column: "ai_agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_agents_code",
                table: "ai_agents",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_chats_ai_agents_ai_agent_id",
                table: "chats",
                column: "ai_agent_id",
                principalTable: "ai_agents",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_chats_ai_agents_ai_agent_id",
                table: "chats");

            migrationBuilder.DropTable(
                name: "ai_agents");

            migrationBuilder.DropIndex(
                name: "ix_chats_ai_agent_id",
                table: "chats");

            migrationBuilder.DropColumn(
                name: "reply_to_message_chat_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "reply_to_message_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "ai_agent_id",
                table: "chats");

            migrationBuilder.AlterColumn<string>(
                name: "text",
                table: "messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4096)",
                oldMaxLength: 4096,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "sticker_id",
                table: "messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
