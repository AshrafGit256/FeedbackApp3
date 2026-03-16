using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGuestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Guests_Guest_id",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_Guest_id",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "Guest_id",
                table: "Feedbacks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Guest_id",
                table: "Feedbacks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_Guest_id",
                table: "Feedbacks",
                column: "Guest_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Guests_Guest_id",
                table: "Feedbacks",
                column: "Guest_id",
                principalTable: "Guests",
                principalColumn: "Id");
        }
    }
}
