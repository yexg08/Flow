using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "staff_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TemporaryPasswordExpiresAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "appointments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Online");

            migrationBuilder.CreateIndex(
                name: "IX_staff_members_UserId",
                table: "staff_members",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_staff_members_AspNetUsers_UserId",
                table: "staff_members",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_staff_members_AspNetUsers_UserId",
                table: "staff_members");

            migrationBuilder.DropIndex(
                name: "IX_staff_members_UserId",
                table: "staff_members");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "staff_members");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TemporaryPasswordExpiresAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "appointments");
        }
    }
}
