using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenoCRM.Migrations
{
    /// <inheritdoc />
    public partial class member_comapnies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Members",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson",
                table: "Members",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberType",
                table: "Members",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Prefix",
                table: "Members",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ContactPerson",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MemberType",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Prefix",
                table: "Members");
        }
    }
}
