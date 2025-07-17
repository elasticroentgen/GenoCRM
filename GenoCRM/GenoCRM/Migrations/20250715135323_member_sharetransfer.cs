using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenoCRM.Migrations
{
    /// <inheritdoc />
    public partial class member_sharetransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TerminationNoticeDate",
                table: "Members",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShareApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovalDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareApprovals_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShareTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromMemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToMemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShareId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovalDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareTransfers_CooperativeShares_ShareId",
                        column: x => x.ShareId,
                        principalTable: "CooperativeShares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShareTransfers_Members_FromMemberId",
                        column: x => x.FromMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShareTransfers_Members_ToMemberId",
                        column: x => x.ToMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShareApprovals_MemberId",
                table: "ShareApprovals",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransfers_FromMemberId",
                table: "ShareTransfers",
                column: "FromMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransfers_ShareId",
                table: "ShareTransfers",
                column: "ShareId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareTransfers_ToMemberId",
                table: "ShareTransfers",
                column: "ToMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShareApprovals");

            migrationBuilder.DropTable(
                name: "ShareTransfers");

            migrationBuilder.DropColumn(
                name: "TerminationNoticeDate",
                table: "Members");
        }
    }
}
