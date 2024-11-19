using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saturn.Bot.Service.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true),
                    sticker_id = table.Column<string>(type: "text", nullable: true),
                    message_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    chat_id = table.Column<long>(type: "bigint", nullable: false),
                    chat_type = table.Column<int>(type: "integer", nullable: false),
                    chat_name = table.Column<string>(type: "text", nullable: true),
                    from_user_id = table.Column<long>(type: "bigint", nullable: true),
                    from_username = table.Column<string>(type: "text", nullable: true),
                    from_first_name = table.Column<string>(type: "text", nullable: true),
                    from_last_name = table.Column<string>(type: "text", nullable: true),
                    update_data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_messages_chat_id",
                table: "messages",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_from_user_id",
                table: "messages",
                column: "from_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages");
        }
    }
}
