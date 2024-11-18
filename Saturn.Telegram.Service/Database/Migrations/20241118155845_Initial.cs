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
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    StickerId = table.Column<string>(type: "text", nullable: true),
                    MessageDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    ChatType = table.Column<int>(type: "integer", nullable: false),
                    ChatName = table.Column<string>(type: "text", nullable: true),
                    FromUserId = table.Column<long>(type: "bigint", nullable: true),
                    FromUsername = table.Column<string>(type: "text", nullable: true),
                    FromFirstName = table.Column<string>(type: "text", nullable: true),
                    FromLastName = table.Column<string>(type: "text", nullable: true),
                    UpdateData = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId",
                table: "Messages",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_FromUserId",
                table: "Messages",
                column: "FromUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");
        }
    }
}
