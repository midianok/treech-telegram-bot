using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saturn.Telegram.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddChatIdToScoresAndKarma : Migration
    {
        private const long DefaultChatId = -1002680016267L;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_namorevo_gore_scores",
                table: "namorevo_gore_scores");

            migrationBuilder.AddColumn<long>(
                name: "chat_id",
                table: "namorevo_gore_scores",
                type: "bigint",
                nullable: false,
                defaultValue: DefaultChatId);

            migrationBuilder.AddPrimaryKey(
                name: "pk_namorevo_gore_scores",
                table: "namorevo_gore_scores",
                columns: ["user_id", "chat_id"]);

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_karma",
                table: "user_karma");

            migrationBuilder.AddColumn<long>(
                name: "chat_id",
                table: "user_karma",
                type: "bigint",
                nullable: false,
                defaultValue: DefaultChatId);

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_karma",
                table: "user_karma",
                columns: ["user_id", "chat_id"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_namorevo_gore_scores",
                table: "namorevo_gore_scores");

            migrationBuilder.DropColumn(
                name: "chat_id",
                table: "namorevo_gore_scores");

            migrationBuilder.AddPrimaryKey(
                name: "pk_namorevo_gore_scores",
                table: "namorevo_gore_scores",
                column: "user_id");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_karma",
                table: "user_karma");

            migrationBuilder.DropColumn(
                name: "chat_id",
                table: "user_karma");

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_karma",
                table: "user_karma",
                column: "user_id");
        }
    }
}
