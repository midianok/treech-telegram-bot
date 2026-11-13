using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Saturn.Telegram.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "messages",
                type: "tsvector",
                nullable: true)
                .Annotation("Npgsql:TsVectorConfig", "russian")
                .Annotation("Npgsql:TsVectorProperties", new[] { "text" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_chat_id_message_date",
                table: "messages",
                columns: new[] { "chat_id", "message_date" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_search_vector",
                table: "messages",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_messages_chat_id_message_date",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "ix_messages_search_vector",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "messages");
        }
    }
}
