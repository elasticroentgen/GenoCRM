using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenoCRM.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGroupPermissionMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupPermissionMappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupPermissionMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GroupName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupPermissionMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupPermissionMappings_GroupName",
                table: "GroupPermissionMappings",
                column: "GroupName");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPermissionMappings_GroupName_Permission",
                table: "GroupPermissionMappings",
                columns: new[] { "GroupName", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupPermissionMappings_Permission",
                table: "GroupPermissionMappings",
                column: "Permission");
        }
    }
}
