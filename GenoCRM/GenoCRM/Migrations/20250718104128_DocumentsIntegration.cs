using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenoCRM.Migrations
{
    /// <inheritdoc />
    public partial class DocumentsIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MemberId",
                table: "Documents",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "ShareId",
                table: "Documents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ShareId",
                table: "Documents",
                column: "ShareId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_CooperativeShares_ShareId",
                table: "Documents",
                column: "ShareId",
                principalTable: "CooperativeShares",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_CooperativeShares_ShareId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ShareId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ShareId",
                table: "Documents");

            migrationBuilder.AlterColumn<int>(
                name: "MemberId",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
