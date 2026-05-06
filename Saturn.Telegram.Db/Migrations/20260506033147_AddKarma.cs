using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Saturn.Telegram.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddKarma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "karma_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_user_id = table.Column<long>(type: "bigint", nullable: false),
                    to_user_id = table.Column<long>(type: "bigint", nullable: false),
                    chat_id = table.Column<long>(type: "bigint", nullable: false),
                    message_id = table.Column<long>(type: "bigint", nullable: false),
                    delta = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_karma_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_karma_changes_users_from_user_id",
                        column: x => x.from_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_karma_changes_users_to_user_id",
                        column: x => x.to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_karma",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_karma", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_karma_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_karma_changes_created_at",
                table: "karma_changes",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_karma_changes_from_user_id",
                table: "karma_changes",
                column: "from_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_karma_changes_to_user_id",
                table: "karma_changes",
                column: "to_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "karma_changes");

            migrationBuilder.DropTable(
                name: "user_karma");
        }
    }
}
