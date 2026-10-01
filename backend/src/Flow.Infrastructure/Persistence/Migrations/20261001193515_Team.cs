using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Team : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_members_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_member_services",
                columns: table => new
                {
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_member_services", x => new { x.StaffMemberId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_staff_member_services_services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_member_services_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_member_services_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "time_off",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_off", x => x.Id);
                    table.CheckConstraint("ck_time_off_range", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                    table.ForeignKey(
                        name: "FK_time_off_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_time_off_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "working_hours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    End = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_working_hours", x => x.Id);
                    table.CheckConstraint("ck_working_hours_range", "\"End\" > \"Start\"");
                    table.ForeignKey(
                        name: "FK_working_hours_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_working_hours_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_member_services_ServiceId",
                table: "staff_member_services",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_member_services_TenantId",
                table: "staff_member_services",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_members_TenantId",
                table: "staff_members",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_time_off_StaffMemberId",
                table: "time_off",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_time_off_TenantId_EndsAtUtc",
                table: "time_off",
                columns: new[] { "TenantId", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_working_hours_StaffMemberId",
                table: "working_hours",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_working_hours_TenantId_StaffMemberId",
                table: "working_hours",
                columns: new[] { "TenantId", "StaffMemberId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_member_services");

            migrationBuilder.DropTable(
                name: "time_off");

            migrationBuilder.DropTable(
                name: "working_hours");

            migrationBuilder.DropTable(
                name: "staff_members");
        }
    }
}
