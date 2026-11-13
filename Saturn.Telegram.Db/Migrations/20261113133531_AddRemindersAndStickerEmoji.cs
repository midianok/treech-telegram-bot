using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Saturn.Telegram.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddRemindersAndStickerEmoji : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sticker_emoji",
                table: "messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reminders",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chat_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    request_message_id = table.Column<long>(type: "bigint", nullable: true),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    remind_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminders", x => x.id);
                    table.ForeignKey(
                        name: "fk_reminders_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reminders_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_chat_id_created_by_user_id",
                table: "reminders",
                columns: new[] { "chat_id", "created_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reminders_created_by_user_id",
                table: "reminders",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_remind_at",
                table: "reminders",
                column: "remind_at",
                filter: "sent_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_user_id",
                table: "reminders",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reminders");

            migrationBuilder.DropColumn(
                name: "sticker_emoji",
                table: "messages");
        }
    }
}
