using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saturn.Telegram.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddCoinBalanceAndTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "coin_balance",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "coin_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    operation = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    external_payment_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coin_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_coin_transactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coin_transactions_external_payment_id",
                table: "coin_transactions",
                column: "external_payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_coin_transactions_user_id",
                table: "coin_transactions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coin_transactions");

            migrationBuilder.DropColumn(
                name: "coin_balance",
                table: "users");
        }
    }
}
