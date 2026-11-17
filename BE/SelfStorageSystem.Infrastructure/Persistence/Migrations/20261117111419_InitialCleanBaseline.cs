using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfStorageSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCleanBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateSequence(
                name: "agreement_no_seq",
                schema: "core",
                startValue: 1001L);

            migrationBuilder.CreateSequence(
                name: "invoice_no_seq",
                schema: "core",
                startValue: 1001L);

            migrationBuilder.CreateSequence(
                name: "reservation_code_seq",
                schema: "core",
                startValue: 1001L);

            migrationBuilder.CreateSequence(
                name: "ticket_no_seq",
                schema: "core",
                startValue: 1001L);

            migrationBuilder.CreateSequence(
                name: "work_order_no_seq",
                schema: "core",
                startValue: 1001L);

            migrationBuilder.CreateTable(
                name: "facilities",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    address_line = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ward = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    district = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    city = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    timezone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "Asia/Ho_Chi_Minh"),
                    opening_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    closing_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__faciliti__3213E83FE0B4338E", x => x.id);
                    table.CheckConstraint("CK_facilities_hours", "[closing_time] IS NULL OR [opening_time] IS NULL OR [closing_time] > [opening_time]");
                    table.CheckConstraint("CK_facilities_latitude", "[latitude] IS NULL OR ([latitude] >= -90 AND [latitude] <= 90)");
                    table.CheckConstraint("CK_facilities_longitude", "[longitude] IS NULL OR ([longitude] >= -180 AND [longitude] <= 180)");
                    table.CheckConstraint("CK_facilities_status", "[status] IN ('draft', 'active', 'inactive', 'maintenance', 'suspended_new_bookings', 'temporarily_closed', 'closed')");
                });

            migrationBuilder.CreateTable(
                name: "integration_events",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    source = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    external_event_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "received"),
                    error_message = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    processed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__integrat__3213E83FAE9A93FF", x => x.id);
                    table.CheckConstraint("CK_integration_events_payload_json", "ISJSON([payload]) = 1");
                    table.CheckConstraint("CK_integration_events_status", "[status] IN ('received', 'processed', 'failed', 'ignored')");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__roles__3213E83F47C336B5", x => x.id);
                    table.CheckConstraint("CK_roles_code", "[code] IN ('storage_customer', 'facility_staff', 'facility_manager', 'system_administrator')");
                });

            migrationBuilder.CreateTable(
                name: "unit_types",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    width_m = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    length_m = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    height_m = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(10,2)", nullable: true, computedColumnSql: "(CONVERT([numeric](10,2),[width_m]*[length_m]))", stored: true),
                    volume_m3 = table.Column<decimal>(type: "numeric(12,2)", nullable: true, computedColumnSql: "(CONVERT([numeric](12,2),([width_m]*[length_m])*[height_m]))", stored: true),
                    climate_controlled = table.Column<bool>(type: "bit", nullable: false),
                    max_weight_kg = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__unit_typ__3213E83F18088A55", x => x.id);
                    table.CheckConstraint("CK_unit_types_dimensions", "[width_m] > 0 AND [length_m] > 0 AND [height_m] > 0");
                    table.CheckConstraint("CK_unit_types_max_weight", "[max_weight_kg] IS NULL OR [max_weight_kg] > 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    phone_number = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    password_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "active"),
                    last_login_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__users__3213E83F69F02F0F", x => x.id);
                    table.CheckConstraint("CK_users_status", "[status] IN ('active', 'locked', 'disabled')");
                    table.CheckConstraint("users_email_normalized", "[email] = lower(LTRIM(RTRIM([email])))");
                });

            migrationBuilder.CreateTable(
                name: "access_points",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    access_point_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    external_device_code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__access_p__3213E83FFEB8BC50", x => x.id);
                    table.CheckConstraint("CK_access_points_status", "[status] IN ('active', 'offline', 'maintenance', 'retired')");
                    table.CheckConstraint("CK_access_points_type", "[access_point_type] IN ('gate', 'building_door', 'floor_door', 'unit_door')");
                    table.ForeignKey(
                        name: "FK__access_po__facil__3E723F9C",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "facility_areas",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_area_id = table.Column<long>(type: "bigint", nullable: true),
                    code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    area_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    display_order = table.Column<int>(type: "int", nullable: false),
                    map_metadata = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__facility__3213E83FEE469076", x => x.id);
                    table.UniqueConstraint("AK_facility_areas_id_facility_id", x => new { x.id, x.facility_id });
                    table.CheckConstraint("CK_facility_areas_area_type", "[area_type] IN ('building', 'floor', 'zone', 'section')");
                    table.CheckConstraint("CK_facility_areas_map_metadata_json", "ISJSON([map_metadata]) = 1");
                    table.CheckConstraint("CK_facility_areas_no_self_parent", "[parent_area_id] IS NULL OR [parent_area_id] <> [id]");
                    table.ForeignKey(
                        name: "FK__facility___facil__01142BA1",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__facility___paren__02084FDA",
                        column: x => x.parent_area_id,
                        principalSchema: "core",
                        principalTable: "facility_areas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__facility_areas__08B54D69",
                        columns: x => new { x.parent_area_id, x.facility_id },
                        principalSchema: "core",
                        principalTable: "facility_areas",
                        principalColumns: new[] { "id", "facility_id" });
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    actor_user_id = table.Column<long>(type: "bigint", nullable: true),
                    actor_role = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    entity_schema = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "core"),
                    entity_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    entity_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    action = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    old_values = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    new_values = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    request_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__audit_lo__3213E83FDE7CB008", x => x.id);
                    table.ForeignKey(
                        name: "FK__audit_log__actor__74CE504D",
                        column: x => x.actor_user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "customer_profiles",
                schema: "core",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    identity_number = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    emergency_contact_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    profile_edit_month = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: true),
                    profile_edit_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__customer__B9BE370F6AA710AD", x => x.user_id);
                    table.CheckConstraint("CK_customer_profiles_edit_count", "[profile_edit_count] >= 0 AND [profile_edit_count] <= 2");
                    table.ForeignKey(
                        name: "FK__customer___user___60A75C0F",
                        column: x => x.user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_profiles",
                schema: "core",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: false),
                    employment_status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__employee__B9BE370FE5C68310", x => x.user_id);
                    table.CheckConstraint("CK_employee_profiles_employment_status", "[employment_status] IN ('active', 'leave', 'terminated')");
                    table.ForeignKey(
                        name: "FK__employee___user___66603565",
                        column: x => x.user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "facility_rates",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    unit_type_id = table.Column<long>(type: "bigint", nullable: false),
                    monthly_rate = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    booking_fee = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__facility__3213E83F925384A1", x => x.id);
                    table.CheckConstraint("CK_facility_rates_amounts", "[monthly_rate] >= 0 AND [deposit_amount] >= 0 AND [booking_fee] >= 0");
                    table.CheckConstraint("CK_facility_rates_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
                    table.ForeignKey(
                        name: "FK__facility___creat__3D2915A8",
                        column: x => x.created_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__facility___facil__37703C52",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__facility___unit___3864608B",
                        column: x => x.unit_type_id,
                        principalSchema: "core",
                        principalTable: "unit_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "fee_rules",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    facility_id = table.Column<long>(type: "bigint", nullable: true),
                    code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    fee_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    calculation_method = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    rate_percent = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    grace_days = table.Column<int>(type: "int", nullable: false),
                    conditions = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__fee_rule__3213E83F5F89760E", x => x.id);
                    table.CheckConstraint("CK_fee_rules_amount", "[amount] IS NULL OR [amount] >= 0");
                    table.CheckConstraint("CK_fee_rules_calculation_method", "[calculation_method] IN ('flat', 'percentage', 'per_day')");
                    table.CheckConstraint("CK_fee_rules_conditions_json", "ISJSON([conditions]) = 1");
                    table.CheckConstraint("CK_fee_rules_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
                    table.CheckConstraint("CK_fee_rules_fee_type", "[fee_type] IN ('late_fee', 'damage_fee', 'lost_key_fee', 'maintenance_fee', 'booking', 'booking_fee', 'tax', 'vat_tax', 'other')");
                    table.CheckConstraint("CK_fee_rules_grace_days", "[grace_days] >= 0");
                    table.CheckConstraint("CK_fee_rules_method_value", "([calculation_method] IN ('flat', 'per_day') AND [amount] IS NOT NULL) OR ([calculation_method] = 'percentage' AND [rate_percent] IS NOT NULL)");
                    table.CheckConstraint("CK_fee_rules_rate_percent", "[rate_percent] IS NULL OR ([rate_percent] > 0 AND [rate_percent] <= 1000)");
                    table.ForeignKey(
                        name: "FK__fee_rules__creat__531856C7",
                        column: x => x.created_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__fee_rules__facil__498EEC8D",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "login_history",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    attempted_email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    result = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ip_address = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__login_hi__3213E83F95DB5DA2", x => x.id);
                    table.CheckConstraint("CK_login_history_result", "[result] IN ('succeeded', 'failed', 'locked', 'blocked')");
                    table.ForeignKey(
                        name: "FK__login_his__user___7993056A",
                        column: x => x.user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    channel = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    template_code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    payload = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "pending"),
                    scheduled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    sent_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    attempts = table.Column<short>(type: "smallint", nullable: false),
                    last_error = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    deduplication_key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__notifica__3213E83F5750E434", x => x.id);
                    table.CheckConstraint("CK_notifications_attempts", "[attempts] >= 0");
                    table.CheckConstraint("CK_notifications_channel", "[channel] IN ('in_app', 'email', 'sms', 'push')");
                    table.CheckConstraint("CK_notifications_payload_json", "ISJSON([payload]) = 1");
                    table.CheckConstraint("CK_notifications_status", "[status] IN ('pending', 'processing', 'sent', 'failed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK__notificat__user___695C9DA1",
                        column: x => x.user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "policy_versions",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    policy_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    version = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__policy_v__3213E83FC5850671", x => x.id);
                    table.CheckConstraint("CK_policy_versions_content_json", "ISJSON([content]) = 1");
                    table.CheckConstraint("CK_policy_versions_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
                    table.CheckConstraint("CK_policy_versions_policy_type", "[policy_type] IN ('rental_terms', 'deposit', 'cancellation', 'renewal', 'move_out', 'overdue')");
                    table.ForeignKey(
                        name: "FK__policy_ve__creat__44CA3770",
                        column: x => x.created_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "price_ranges",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    unit_type_id = table.Column<long>(type: "bigint", nullable: false),
                    min_monthly_rate = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    max_monthly_rate = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__price_ra__3213E83F2173EE04", x => x.id);
                    table.CheckConstraint("CK_price_ranges_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
                    table.CheckConstraint("CK_price_ranges_rates", "[min_monthly_rate] >= 0 AND [max_monthly_rate] >= [min_monthly_rate]");
                    table.ForeignKey(
                        name: "FK__price_ran__creat__31B762FC",
                        column: x => x.created_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__price_ran__unit___2FCF1A8A",
                        column: x => x.unit_type_id,
                        principalSchema: "core",
                        principalTable: "unit_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "promotions",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    discount_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    max_discount_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    usage_limit = table.Column<int>(type: "int", nullable: true),
                    per_customer_limit = table.Column<int>(type: "int", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__promotio__3213E83F39C628D1", x => x.id);
                    table.CheckConstraint("CK_promotions_dates", "[valid_to] > [valid_from]");
                    table.CheckConstraint("CK_promotions_discount_type", "[discount_type] IN ('fixed', 'percentage', 'free_days')");
                    table.CheckConstraint("CK_promotions_limits", "([usage_limit] IS NULL OR [usage_limit] > 0) AND ([per_customer_limit] IS NULL OR [per_customer_limit] > 0)");
                    table.CheckConstraint("CK_promotions_percentage_cap", "[discount_type] <> 'percentage' OR [discount_value] <= 100");
                    table.CheckConstraint("CK_promotions_values", "[discount_value] > 0 AND ([max_discount_amount] IS NULL OR [max_discount_amount] > 0)");
                    table.ForeignKey(
                        name: "FK__promotion__creat__5F7E2DAC",
                        column: x => x.created_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "staff_shifts",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    shift_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "planned"),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__staff_sh__3213E83F1E638B20", x => x.id);
                    table.CheckConstraint("CK_staff_shifts_dates", "[ends_at] > [starts_at]");
                    table.CheckConstraint("CK_staff_shifts_status", "[status] IN ('planned', 'open', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK__staff_shi__creat__7814D14C",
                        column: x => x.created_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__staff_shi__facil__753864A1",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "core",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<short>(type: "smallint", nullable: false),
                    granted_by = table.Column<long>(type: "bigint", nullable: true),
                    granted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__user_rol__6EDEA1530C21B709", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK__user_role__grant__5CD6CB2B",
                        column: x => x.granted_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__user_role__role___5BE2A6F2",
                        column: x => x.role_id,
                        principalSchema: "core",
                        principalTable: "roles",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__user_role__user___5AEE82B9",
                        column: x => x.user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "storage_units",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    unit_type_id = table.Column<long>(type: "bigint", nullable: false),
                    area_id = table.Column<long>(type: "bigint", nullable: true),
                    unit_code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    floor_label = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    zone_label = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    physical_status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "available"),
                    is_listed = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__storage___3213E83F401545AC", x => x.id);
                    table.CheckConstraint("CK_storage_units_physical_status", "[physical_status] IN ('available', 'reserved', 'occupied', 'pending_inspection', 'cleaning', 'maintenance', 'damaged', 'out_of_service')");
                    table.ForeignKey(
                        name: "FK__storage_u__area___1AD3FDA4",
                        column: x => x.area_id,
                        principalSchema: "core",
                        principalTable: "facility_areas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__storage_u__facil__18EBB532",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__storage_u__unit___19DFD96B",
                        column: x => x.unit_type_id,
                        principalSchema: "core",
                        principalTable: "unit_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "staff_facility_assignments",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    assignment_role = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    assigned_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__staff_fa__3213E83FFD9A71D4", x => x.id);
                    table.CheckConstraint("CK_staff_facility_assignments_period", "[ends_at] IS NULL OR [ends_at] > [starts_at]");
                    table.CheckConstraint("CK_staff_facility_assignments_role", "[assignment_role] IN ('facility_staff', 'facility_manager')");
                    table.ForeignKey(
                        name: "FK__staff_fac__assig__7A672E12",
                        column: x => x.assigned_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__staff_fac__emplo__778AC167",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__staff_fac__facil__787EE5A0",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    reservation_code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    unit_type_id = table.Column<long>(type: "bigint", nullable: false),
                    facility_rate_id = table.Column<long>(type: "bigint", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    monthly_rate_snapshot = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    deposit_snapshot = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    booking_fee_snapshot = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    discount_snapshot = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    quoted_total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    hold_until = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "pending"),
                    confirmed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cancellation_reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__reservat__3213E83FB121B783", x => x.id);
                    table.CheckConstraint("CK_reservations_amounts", "[monthly_rate_snapshot] >= 0 AND [deposit_snapshot] >= 0 AND [booking_fee_snapshot] >= 0 AND [discount_snapshot] >= 0 AND [quoted_total] >= 0");
                    table.CheckConstraint("CK_reservations_cancelled_at", "([status] = 'cancelled' AND [cancelled_at] IS NOT NULL) OR [status] <> 'cancelled'");
                    table.CheckConstraint("CK_reservations_dates", "[end_date] > [start_date]");
                    table.CheckConstraint("CK_reservations_status", "[status] IN ('pending', 'awaiting_deposit', 'confirmed', 'checked_in', 'converted', 'completed', 'no_show', 'cancelled', 'expired')");
                    table.CheckConstraint("reservations_hold_max_15m_ck", "[hold_until] > [created_at] AND [hold_until] <= DATEADD(minute, 15, [created_at])");
                    table.CheckConstraint("reservations_rental_term_ck", "[end_date] >= DATEADD(month, 1, [start_date]) AND [end_date] <= DATEADD(month, 12, [start_date])");
                    table.ForeignKey(
                        name: "FK__reservati__custo__6DCC4D03",
                        column: x => x.customer_id,
                        principalSchema: "core",
                        principalTable: "customer_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__reservati__facil__6EC0713C",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__reservati__facil__70A8B9AE",
                        column: x => x.facility_rate_id,
                        principalSchema: "core",
                        principalTable: "facility_rates",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__reservati__unit___6FB49575",
                        column: x => x.unit_type_id,
                        principalSchema: "core",
                        principalTable: "unit_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "promotion_rules",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    promotion_id = table.Column<long>(type: "bigint", nullable: false),
                    rule_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "eq"),
                    rule_value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__promotio__3213E83F600E839C", x => x.id);
                    table.CheckConstraint("CK_promotion_rules_operator", "[operator] IN ('eq', 'gte', 'lte', 'in')");
                    table.CheckConstraint("CK_promotion_rules_rule_type", "[rule_type] IN ('minimum_months', 'month', 'minimum_amount', 'facility', 'unit_type', 'new_customer')");
                    table.ForeignKey(
                        name: "FK__promotion__promo__662B2B3B",
                        column: x => x.promotion_id,
                        principalSchema: "core",
                        principalTable: "promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shift_assignments",
                schema: "core",
                columns: table => new
                {
                    shift_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    duty_role = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    check_in_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    check_out_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "scheduled")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__shift_as__E774929A092DBDEE", x => new { x.shift_id, x.employee_id });
                    table.CheckConstraint("CK_shift_assignments_check_times", "[check_out_at] IS NULL OR ([check_in_at] IS NOT NULL AND [check_out_at] >= [check_in_at])");
                    table.CheckConstraint("CK_shift_assignments_duty_role", "[duty_role] IN ('staff', 'shift_lead', 'manager_on_call')");
                    table.CheckConstraint("CK_shift_assignments_status", "[status] IN ('scheduled', 'checked_in', 'checked_out', 'absent')");
                    table.ForeignKey(
                        name: "FK__shift_ass__emplo__7DCDAAA2",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__shift_ass__shift__7CD98669",
                        column: x => x.shift_id,
                        principalSchema: "core",
                        principalTable: "staff_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unit_map_positions",
                schema: "core",
                columns: table => new
                {
                    unit_id = table.Column<long>(type: "bigint", nullable: false),
                    area_id = table.Column<long>(type: "bigint", nullable: false),
                    x = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    y = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    width = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    height = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    rotation_degrees = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    metadata = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__unit_map__D3AF5BD7302169A8", x => x.unit_id);
                    table.CheckConstraint("CK_unit_map_positions_dimensions", "[width] > 0 AND [height] > 0");
                    table.CheckConstraint("CK_unit_map_positions_metadata_json", "ISJSON([metadata]) = 1");
                    table.ForeignKey(
                        name: "FK__unit_map___area___236943A5",
                        column: x => x.area_id,
                        principalSchema: "core",
                        principalTable: "facility_areas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_map___unit___22751F6C",
                        column: x => x.unit_id,
                        principalSchema: "core",
                        principalTable: "storage_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unit_status_history",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    storage_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    old_status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    new_status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    changed_by = table.Column<long>(type: "bigint", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__unit_sta__3213E83F20E95C47", x => x.id);
                    table.ForeignKey(
                        name: "FK__unit_stat__chang__2BFE89A6",
                        column: x => x.changed_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_stat__stora__2B0A656D",
                        column: x => x.storage_unit_id,
                        principalSchema: "core",
                        principalTable: "storage_units",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "identity_verifications",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    reservation_id = table.Column<long>(type: "bigint", nullable: false),
                    verified_by = table.Column<long>(type: "bigint", nullable: false),
                    verification_method = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    document_fingerprint = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    result = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__identity__3213E83FFD6D658F", x => x.id);
                    table.CheckConstraint("CK_identity_verifications_method", "[verification_method] IN ('government_id', 'passport', 'digital_identity', 'manual')");
                    table.CheckConstraint("CK_identity_verifications_result", "[result] IN ('verified', 'rejected')");
                    table.ForeignKey(
                        name: "FK__identity___reser__2BC97F7C",
                        column: x => x.reservation_id,
                        principalSchema: "core",
                        principalTable: "reservations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__identity___verif__2CBDA3B5",
                        column: x => x.verified_by,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "rental_agreements",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    agreement_no = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    reservation_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    policy_version_id = table.Column<long>(type: "bigint", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    monthly_rate_snapshot = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    deposit_snapshot = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    deposit_balance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "draft"),
                    signed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    checked_in_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    checked_out_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    actual_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    negative_debt_notified_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    overdue_bank_reminder_sent_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__rental_a__3213E83F74E7BFF0", x => x.id);
                    table.CheckConstraint("CK_rental_agreements_amounts", "[monthly_rate_snapshot] >= 0 AND [deposit_snapshot] >= 0");
                    table.CheckConstraint("CK_rental_agreements_checkout", "([checked_out_at] IS NULL) OR ([checked_in_at] IS NOT NULL AND [checked_out_at] >= [checked_in_at])");
                    table.CheckConstraint("CK_rental_agreements_dates", "[end_date] > [start_date]");
                    table.CheckConstraint("CK_rental_agreements_status", "[status] IN ('draft', 'scheduled', 'active', 'expiring_soon', 'extended', 'overdue', 'defaulted', 'move_out_scheduled', 'checkout_pending', 'expired', 'terminated', 'completed', 'closed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK__rental_ag__custo__04AFB25B",
                        column: x => x.customer_id,
                        principalSchema: "core",
                        principalTable: "customer_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__rental_ag__facil__05A3D694",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__rental_ag__polic__0697FACD",
                        column: x => x.policy_version_id,
                        principalSchema: "core",
                        principalTable: "policy_versions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__rental_ag__reser__03BB8E22",
                        column: x => x.reservation_id,
                        principalSchema: "core",
                        principalTable: "reservations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    reservation_id = table.Column<long>(type: "bigint", nullable: true),
                    agreement_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_staff_id = table.Column<long>(type: "bigint", nullable: true),
                    appointment_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "scheduled"),
                    notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__appointm__3213E83F154E5845", x => x.id);
                    table.CheckConstraint("CK_appointments_dates", "[ends_at] > [starts_at]");
                    table.CheckConstraint("CK_appointments_status", "[status] IN ('scheduled', 'confirmed', 'in_progress', 'completed', 'cancelled', 'no_show')");
                    table.CheckConstraint("CK_appointments_type", "[appointment_type] IN ('check_in', 'check_out', 'transfer', 'support', 'inspection')");
                    table.CheckConstraint("CK_appointments_xor_target", "(CASE WHEN [reservation_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [agreement_id] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK__appointme__agree__214BF109",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__appointme__assig__22401542",
                        column: x => x.assigned_staff_id,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__appointme__facil__1F63A897",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__appointme__reser__2057CCD0",
                        column: x => x.reservation_id,
                        principalSchema: "core",
                        principalTable: "reservations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "authorized_access_members",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    agreement_id = table.Column<long>(type: "bigint", nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    identity_fingerprint = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    relationship_to_customer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    valid_to = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "active"),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__authoriz__3213E83F51D983B0", x => x.id);
                    table.CheckConstraint("CK_authorized_access_members_dates", "[valid_to] IS NULL OR [valid_to] > [valid_from]");
                    table.CheckConstraint("CK_authorized_access_members_status", "[status] IN ('pending', 'active', 'revoked', 'expired')");
                    table.ForeignKey(
                        name: "FK__authorize__agree__4D2A7347",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__authorize__creat__50FB042B",
                        column: x => x.created_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "inspections",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    storage_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    reservation_id = table.Column<long>(type: "bigint", nullable: true),
                    agreement_id = table.Column<long>(type: "bigint", nullable: true),
                    inspected_by = table.Column<long>(type: "bigint", nullable: false),
                    inspection_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "draft"),
                    overall_condition = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    summary = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    inspected_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__inspecti__3213E83F24198D74", x => x.id);
                    table.CheckConstraint("CK_inspections_inspected_at", "([status] = 'draft' AND [inspected_at] IS NULL) OR ([status] <> 'draft' AND [inspected_at] IS NOT NULL)");
                    table.CheckConstraint("CK_inspections_overall_condition", "[overall_condition] IS NULL OR ([overall_condition] IN ('good', 'acceptable', 'damaged', 'unsafe'))");
                    table.CheckConstraint("CK_inspections_status", "[status] IN ('draft', 'completed', 'failed')");
                    table.CheckConstraint("CK_inspections_type", "[inspection_type] IN ('check_in', 'check_out', 'transfer', 'security', 'fire_safety', 'routine', 'incident')");
                    table.CheckConstraint("CK_inspections_unit_required", "[inspection_type] NOT IN ('check_in', 'check_out', 'transfer') OR [storage_unit_id] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK__inspectio__agree__3552E9B6",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__inspectio__facil__32767D0B",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__inspectio__inspe__36470DEF",
                        column: x => x.inspected_by,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__inspectio__reser__345EC57D",
                        column: x => x.reservation_id,
                        principalSchema: "core",
                        principalTable: "reservations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__inspectio__stora__336AA144",
                        column: x => x.storage_unit_id,
                        principalSchema: "core",
                        principalTable: "storage_units",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "rental_renewals",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    agreement_id = table.Column<long>(type: "bigint", nullable: false),
                    requested_by = table.Column<long>(type: "bigint", nullable: false),
                    old_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    requested_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    approved_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    old_monthly_rate = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    new_monthly_rate = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "pending_payment"),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__rental_r__3213E83FD6FCC618", x => x.id);
                    table.CheckConstraint("CK_rental_renewals_dates", "[requested_end_date] > [old_end_date] AND ([approved_end_date] IS NULL OR [approved_end_date] > [old_end_date])");
                    table.CheckConstraint("CK_rental_renewals_rates", "[old_monthly_rate] >= 0 AND ([new_monthly_rate] IS NULL OR [new_monthly_rate] >= 0)");
                    table.CheckConstraint("CK_rental_renewals_status", "[status] IN ('pending_payment', 'paid', 'approved', 'rejected', 'cancelled')");
                    table.ForeignKey(
                        name: "FK__rental_re__agree__55BFB948",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__rental_re__reque__56B3DD81",
                        column: x => x.requested_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__rental_re__revie__5B78929E",
                        column: x => x.reviewed_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "stored_items",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    agreement_id = table.Column<long>(type: "bigint", nullable: false),
                    item_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    estimated_value = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    risk_classification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "standard"),
                    photo_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    location_note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    declared_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stored_items", x => x.id);
                    table.CheckConstraint("CK_stored_items_estimated_value", "[estimated_value] IS NULL OR [estimated_value] >= 0");
                    table.CheckConstraint("CK_stored_items_quantity", "[quantity] > 0");
                    table.ForeignKey(
                        name: "FK_stored_items_rental_agreements_agreement_id",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "support_tickets",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticket_no = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    agreement_id = table.Column<long>(type: "bigint", nullable: true),
                    storage_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    category = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    priority = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "normal"),
                    subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "open"),
                    resolution = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__support___3213E83F1DA69E75", x => x.id);
                    table.CheckConstraint("CK_support_tickets_category", "[category] IN ('unit', 'access', 'payment', 'stored_item', 'maintenance', 'other')");
                    table.CheckConstraint("CK_support_tickets_description_non_empty", "LTRIM(RTRIM([description])) <> ''");
                    table.CheckConstraint("CK_support_tickets_priority", "[priority] IN ('low', 'normal', 'high', 'urgent')");
                    table.CheckConstraint("CK_support_tickets_resolved_consistency", "([status] IN ('resolved', 'closed') AND [resolved_at] IS NOT NULL AND [resolution] IS NOT NULL) OR [status] NOT IN ('resolved', 'closed')");
                    table.CheckConstraint("CK_support_tickets_status", "[status] IN ('open', 'pending_manager_review', 'assigned', 'in_progress', 'waiting_for_customer', 'waiting_for_maintenance', 'severe_need_relocation', 'assessed', 'resolved', 'closed', 'cancelled')");
                    table.CheckConstraint("CK_support_tickets_subject_non_empty", "LTRIM(RTRIM([subject])) <> ''");
                    table.ForeignKey(
                        name: "FK__support_t__agree__084B3915",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__support_t__custo__0662F0A3",
                        column: x => x.customer_id,
                        principalSchema: "core",
                        principalTable: "customer_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__support_t__facil__075714DC",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__support_t__stora__093F5D4E",
                        column: x => x.storage_unit_id,
                        principalSchema: "core",
                        principalTable: "storage_units",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "unit_allocations",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    storage_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    reservation_id = table.Column<long>(type: "bigint", nullable: true),
                    agreement_id = table.Column<long>(type: "bigint", nullable: true),
                    allocation_kind = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    allocation_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    allocation_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "active"),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    assigned_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ended_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__unit_all__3213E83F3E8D7AD4", x => x.id);
                    table.CheckConstraint("CK_unit_allocations_dates", "[allocation_end_date] > [allocation_start_date]");
                    table.CheckConstraint("CK_unit_allocations_ended_at", "([status] = 'active' AND [ended_at] IS NULL) OR ([status] <> 'active' AND [ended_at] IS NOT NULL)");
                    table.CheckConstraint("CK_unit_allocations_kind", "[allocation_kind] IN ('reservation_hold', 'rental', 'transfer')");
                    table.CheckConstraint("CK_unit_allocations_status", "[status] IN ('active', 'consumed', 'released', 'expired')");
                    table.CheckConstraint("CK_unit_allocations_xor_target", "(CASE WHEN [reservation_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [agreement_id] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK__unit_allo__agree__14E61A24",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_allo__assig__18B6AB08",
                        column: x => x.assigned_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_allo__reser__13F1F5EB",
                        column: x => x.reservation_id,
                        principalSchema: "core",
                        principalTable: "reservations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_allo__stora__12FDD1B2",
                        column: x => x.storage_unit_id,
                        principalSchema: "core",
                        principalTable: "storage_units",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "unit_transfer_requests",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    agreement_id = table.Column<long>(type: "bigint", nullable: false),
                    requested_unit_type_id = table.Column<long>(type: "bigint", nullable: false),
                    requested_facility_id = table.Column<long>(type: "bigint", nullable: false),
                    from_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    to_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    transfer_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "upgrade"),
                    target_invoice_id = table.Column<long>(type: "bigint", nullable: true),
                    requested_effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    move_out_date = table.Column<DateOnly>(type: "date", nullable: false),
                    move_in_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reschedule_count = table.Column<int>(type: "int", nullable: false),
                    diff_rent_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    diff_deposit_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    cleaning_fee = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    penalty_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    credit_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_paid_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "pending_payment"),
                    requested_by = table.Column<long>(type: "bigint", nullable: false),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__unit_tra__3213E83F997BA96F", x => x.id);
                    table.CheckConstraint("CK_unit_transfer_requests_status", "[status] IN ('pending_payment', 'paid', 'moved_out_in_transit', 'cancelled_overdue_move_out', 'pending', 'approved', 'scheduled', 'completed', 'rejected', 'cancelled')");
                    table.CheckConstraint("CK_unit_transfer_requests_type", "[transfer_type] IN ('upgrade', 'downgrade', 'same_tier', 'relocation')");
                    table.ForeignKey(
                        name: "FK__unit_tran__agree__61316BF4",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_tran__from___6319B466",
                        column: x => x.from_unit_id,
                        principalSchema: "core",
                        principalTable: "storage_units",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_tran__reque__6225902D",
                        column: x => x.requested_unit_type_id,
                        principalSchema: "core",
                        principalTable: "unit_types",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_tran__reque__66EA454A",
                        column: x => x.requested_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_tran__revie__67DE6983",
                        column: x => x.reviewed_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__unit_tran__to_un__640DD89F",
                        column: x => x.to_unit_id,
                        principalSchema: "core",
                        principalTable: "storage_units",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_unit_transfer_requests_requested_facility",
                        column: x => x.requested_facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "move_out_requests",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    agreement_id = table.Column<long>(type: "bigint", nullable: false),
                    requested_by = table.Column<long>(type: "bigint", nullable: false),
                    requested_move_out_date = table.Column<DateOnly>(type: "date", nullable: false),
                    appointment_id = table.Column<long>(type: "bigint", nullable: true),
                    bank_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    bank_account_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    bank_account_holder = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    bank_submitted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    damage_deduction_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false, defaultValue: 0m),
                    overdue_deduction_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false, defaultValue: 0m),
                    net_refund_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "requested"),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    finalized_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__move_out__3213E83F18F51BD9", x => x.id);
                    table.CheckConstraint("CK_move_out_requests_finalized_at", "([status] IN ('completed', 'cancelled') AND [finalized_at] IS NOT NULL) OR ([status] NOT IN ('completed', 'cancelled') AND [finalized_at] IS NULL)");
                    table.CheckConstraint("CK_move_out_requests_status", "[status] IN ('requested', 'scheduled', 'in_progress', 'inspection_pending', 'settlement_pending', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK__move_out___agree__6BAEFA67",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__move_out___appoi__6D9742D9",
                        column: x => x.appointment_id,
                        principalSchema: "core",
                        principalTable: "appointments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__move_out___reque__6CA31EA0",
                        column: x => x.requested_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "access_credentials",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    agreement_id = table.Column<long>(type: "bigint", nullable: false),
                    authorized_member_id = table.Column<long>(type: "bigint", nullable: true),
                    credential_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    external_secret_ref = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    secret_digest = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    display_hint = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    expires_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "pending"),
                    issued_by = table.Column<long>(type: "bigint", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__access_c__3213E83FDE7FE5B7", x => x.id);
                    table.CheckConstraint("CK_access_credentials_expiry", "[expires_at] IS NULL OR [expires_at] > [issued_at]");
                    table.CheckConstraint("CK_access_credentials_revoked", "([status] = 'revoked' AND [revoked_at] IS NOT NULL) OR [status] <> 'revoked'");
                    table.CheckConstraint("CK_access_credentials_status", "[status] IN ('pending', 'active', 'suspended', 'revoked', 'expired')");
                    table.CheckConstraint("CK_access_credentials_type", "[credential_type] IN ('pin', 'card', 'qr', 'key', 'mobile')");
                    table.ForeignKey(
                        name: "FK__access_cr__agree__442B18F2",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__access_cr__autho__451F3D2B",
                        column: x => x.authorized_member_id,
                        principalSchema: "core",
                        principalTable: "authorized_access_members",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__access_cr__issue__49E3F248",
                        column: x => x.issued_by,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "inspection_items",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    inspection_id = table.Column<long>(type: "bigint", nullable: false),
                    item_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    condition = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    photo_url = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    charge_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__inspecti__3213E83F1C247A95", x => x.id);
                    table.CheckConstraint("CK_inspection_items_charge", "[charge_amount] >= 0");
                    table.CheckConstraint("CK_inspection_items_condition", "[condition] IN ('good', 'acceptable', 'damaged', 'missing', 'not_applicable')");
                    table.ForeignKey(
                        name: "FK__inspectio__inspe__3FD07829",
                        column: x => x.inspection_id,
                        principalSchema: "core",
                        principalTable: "inspections",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "maintenance_work_orders",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    work_order_no = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    storage_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    source_ticket_id = table.Column<long>(type: "bigint", nullable: true),
                    source_inspection_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    priority = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "normal"),
                    blocks_booking = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "open"),
                    estimated_cost = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    actual_cost = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    opened_by = table.Column<long>(type: "bigint", nullable: false),
                    verified_by = table.Column<long>(type: "bigint", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__maintena__3213E83F73AEF50C", x => x.id);
                    table.CheckConstraint("CK_maintenance_work_orders_completed", "([status] = 'completed' AND [completed_at] IS NOT NULL) OR [status] <> 'completed'");
                    table.CheckConstraint("CK_maintenance_work_orders_costs", "([estimated_cost] IS NULL OR [estimated_cost] >= 0) AND ([actual_cost] IS NULL OR [actual_cost] >= 0)");
                    table.CheckConstraint("CK_maintenance_work_orders_priority", "[priority] IN ('low', 'normal', 'high', 'urgent')");
                    table.CheckConstraint("CK_maintenance_work_orders_status", "[status] IN ('open', 'assigned', 'in_progress', 'on_hold', 'awaiting_verification', 'completed', 'cancelled')");
                    table.CheckConstraint("CK_maintenance_work_orders_times", "[completed_at] IS NULL OR ([started_at] IS NOT NULL AND [completed_at] >= [started_at])");
                    table.ForeignKey(
                        name: "FK__maintenan__assig__1E05700A",
                        column: x => x.assigned_employee_id,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__maintenan__facil__1A34DF26",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__maintenan__opene__25A691D2",
                        column: x => x.opened_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__maintenan__sourc__1C1D2798",
                        column: x => x.source_ticket_id,
                        principalSchema: "core",
                        principalTable: "support_tickets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__maintenan__sourc__1D114BD1",
                        column: x => x.source_inspection_id,
                        principalSchema: "core",
                        principalTable: "inspections",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__maintenan__stora__1B29035F",
                        column: x => x.storage_unit_id,
                        principalSchema: "core",
                        principalTable: "storage_units",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__maintenan__verif__269AB60B",
                        column: x => x.verified_by,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateTable(
                name: "service_ratings",
                schema: "core",
                columns: table => new
                {
                    ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    score = table.Column<short>(type: "smallint", nullable: false),
                    comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__service___D596F96B2887DCB4", x => x.ticket_id);
                    table.CheckConstraint("CK_service_ratings_score", "[score] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK__service_r__custo__3F9B6DFF",
                        column: x => x.customer_id,
                        principalSchema: "core",
                        principalTable: "customer_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__service_r__ticke__3EA749C6",
                        column: x => x.ticket_id,
                        principalSchema: "core",
                        principalTable: "support_tickets",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ticket_assignments",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_by = table.Column<long>(type: "bigint", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ended_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    end_reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ticket_a__3213E83FD8FED18D", x => x.id);
                    table.CheckConstraint("CK_ticket_assignments_dates", "[ended_at] IS NULL OR [ended_at] >= [assigned_at]");
                    table.ForeignKey(
                        name: "FK__ticket_as__assig__1975C517",
                        column: x => x.assigned_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__ticket_as__emplo__1881A0DE",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__ticket_as__ticke__178D7CA5",
                        column: x => x.ticket_id,
                        principalSchema: "core",
                        principalTable: "support_tickets",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ticket_charge_proposals",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    proposed_by = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "pending"),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ticket_c__3213E83F9CB5A852", x => x.id);
                    table.CheckConstraint("CK_ticket_charge_proposals_amount", "[amount] > 0");
                    table.CheckConstraint("CK_ticket_charge_proposals_status", "[status] IN ('pending', 'approved', 'rejected', 'invoiced', 'cancelled')");
                    table.ForeignKey(
                        name: "FK__ticket_ch__propo__314D4EA8",
                        column: x => x.proposed_by,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__ticket_ch__ticke__30592A6F",
                        column: x => x.ticket_id,
                        principalSchema: "core",
                        principalTable: "support_tickets",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ticket_messages",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    author_user_id = table.Column<long>(type: "bigint", nullable: true),
                    body = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    is_internal = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ticket_m__3213E83F379E3A8F", x => x.id);
                    table.UniqueConstraint("AK_ticket_messages_id_ticket_id", x => new { x.id, x.ticket_id });
                    table.CheckConstraint("CK_ticket_messages_body_non_empty", "LTRIM(RTRIM([body])) <> ''");
                    table.ForeignKey(
                        name: "FK__ticket_me__autho__2022C2A6",
                        column: x => x.author_user_id,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__ticket_me__ticke__1F2E9E6D",
                        column: x => x.ticket_id,
                        principalSchema: "core",
                        principalTable: "support_tickets",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "handover_records",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    agreement_id = table.Column<long>(type: "bigint", nullable: false),
                    unit_allocation_id = table.Column<long>(type: "bigint", nullable: false),
                    inspection_id = table.Column<long>(type: "bigint", nullable: false),
                    handled_by = table.Column<long>(type: "bigint", nullable: false),
                    handover_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    customer_signature_ref = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    staff_signature_ref = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    customer_signed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    staff_signed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__handover__3213E83F4B215F3D", x => x.id);
                    table.CheckConstraint("CK_handover_records_type", "[handover_type] IN ('check_in', 'check_out', 'transfer')");
                    table.ForeignKey(
                        name: "FK__handover___agree__4589517F",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__handover___handl__4865BE2A",
                        column: x => x.handled_by,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__handover___inspe__477199F1",
                        column: x => x.inspection_id,
                        principalSchema: "core",
                        principalTable: "inspections",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__handover___unit___467D75B8",
                        column: x => x.unit_allocation_id,
                        principalSchema: "core",
                        principalTable: "unit_allocations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "access_events",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    credential_id = table.Column<long>(type: "bigint", nullable: true),
                    access_point_id = table.Column<long>(type: "bigint", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    result = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    external_event_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    metadata = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__access_e__3213E83FC1162516", x => x.id);
                    table.CheckConstraint("CK_access_events_metadata_json", "ISJSON([metadata]) = 1");
                    table.CheckConstraint("CK_access_events_result", "[result] IN ('granted', 'denied', 'error')");
                    table.ForeignKey(
                        name: "FK__access_ev__acces__51851410",
                        column: x => x.access_point_id,
                        principalSchema: "core",
                        principalTable: "access_points",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__access_ev__crede__5090EFD7",
                        column: x => x.credential_id,
                        principalSchema: "core",
                        principalTable: "access_credentials",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "staff_tasks",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    facility_id = table.Column<long>(type: "bigint", nullable: false),
                    shift_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    ticket_id = table.Column<long>(type: "bigint", nullable: true),
                    maintenance_work_order_id = table.Column<long>(type: "bigint", nullable: true),
                    task_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "todo"),
                    progress_percent = table.Column<short>(type: "smallint", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__staff_ta__3213E83FFDB96EA1", x => x.id);
                    table.CheckConstraint("CK_staff_tasks_done_progress", "([status] = 'done' AND [progress_percent] = 100) OR [status] <> 'done'");
                    table.CheckConstraint("CK_staff_tasks_progress", "[progress_percent] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_staff_tasks_status", "[status] IN ('todo', 'in_progress', 'blocked', 'done', 'cancelled')");
                    table.CheckConstraint("CK_staff_tasks_task_type", "[task_type] IN ('check_in', 'check_out', 'inspection', 'maintenance', 'support', 'other')");
                    table.ForeignKey(
                        name: "FK__staff_tas__assig__30242045",
                        column: x => x.assigned_employee_id,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__staff_tas__creat__37C5420D",
                        column: x => x.created_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__staff_tas__facil__2E3BD7D3",
                        column: x => x.facility_id,
                        principalSchema: "core",
                        principalTable: "facilities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__staff_tas__maint__320C68B7",
                        column: x => x.maintenance_work_order_id,
                        principalSchema: "core",
                        principalTable: "maintenance_work_orders",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__staff_tas__shift__2F2FFC0C",
                        column: x => x.shift_id,
                        principalSchema: "core",
                        principalTable: "staff_shifts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__staff_tas__ticke__3118447E",
                        column: x => x.ticket_id,
                        principalSchema: "core",
                        principalTable: "support_tickets",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_no = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    reservation_id = table.Column<long>(type: "bigint", nullable: true),
                    agreement_id = table.Column<long>(type: "bigint", nullable: true),
                    ticket_charge_proposal_id = table.Column<long>(type: "bigint", nullable: true),
                    billing_period = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "VND"),
                    subtotal_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "draft"),
                    opened_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    voided_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__invoices__3213E83F6609AFEF", x => x.id);
                    table.CheckConstraint("CK_invoices_amounts", "[subtotal_amount] >= 0 AND [discount_amount] >= 0 AND [tax_amount] >= 0 AND [total_amount] >= 0 AND [paid_amount] >= 0 AND [paid_amount] <= [total_amount]");
                    table.CheckConstraint("CK_invoices_currency_upper", "[currency] = upper([currency])");
                    table.CheckConstraint("CK_invoices_due_date", "[due_date] >= [issue_date]");
                    table.CheckConstraint("CK_invoices_status", "[status] IN ('draft', 'open', 'partially_paid', 'paid', 'overdue', 'voided')");
                    table.CheckConstraint("CK_invoices_target_link", "(CASE WHEN [reservation_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [agreement_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [ticket_charge_proposal_id] IS NULL THEN 0 ELSE 1 END) >= 1");
                    table.ForeignKey(
                        name: "FK__invoices__agreem__4830B400",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__invoices__custom__46486B8E",
                        column: x => x.customer_id,
                        principalSchema: "core",
                        principalTable: "customer_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__invoices__reserv__473C8FC7",
                        column: x => x.reservation_id,
                        principalSchema: "core",
                        principalTable: "reservations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__invoices__ticket__4924D839",
                        column: x => x.ticket_charge_proposal_id,
                        principalSchema: "core",
                        principalTable: "ticket_charge_proposals",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ticket_charge_approvals",
                schema: "core",
                columns: table => new
                {
                    proposal_id = table.Column<long>(type: "bigint", nullable: false),
                    decision = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    decided_by = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ticket_c__A7BC641C435D978F", x => x.proposal_id);
                    table.CheckConstraint("CK_ticket_charge_approvals_decision", "[decision] IN ('approved', 'rejected')");
                    table.ForeignKey(
                        name: "FK__ticket_ch__decid__3AD6B8E2",
                        column: x => x.decided_by,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__ticket_ch__propo__38EE7070",
                        column: x => x.proposal_id,
                        principalSchema: "core",
                        principalTable: "ticket_charge_proposals",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ticket_attachments",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    message_id = table.Column<long>(type: "bigint", nullable: true),
                    uploaded_by = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    mime_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    object_url = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    sha256 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ticket_a__3213E83FEB7ACA3B", x => x.id);
                    table.CheckConstraint("CK_ticket_attachments_non_empty", "LTRIM(RTRIM([file_name])) <> '' AND LTRIM(RTRIM([mime_type])) <> '' AND LTRIM(RTRIM([object_url])) <> ''");
                    table.CheckConstraint("CK_ticket_attachments_size", "[file_size_bytes] > 0");
                    table.ForeignKey(
                        name: "FK__ticket_at__messa__26CFC035",
                        column: x => x.message_id,
                        principalSchema: "core",
                        principalTable: "ticket_messages",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__ticket_at__ticke__25DB9BFC",
                        column: x => x.ticket_id,
                        principalSchema: "core",
                        principalTable: "support_tickets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__ticket_at__uploa__27C3E46E",
                        column: x => x.uploaded_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__ticket_attachmen__2AA05119",
                        columns: x => new { x.message_id, x.ticket_id },
                        principalSchema: "core",
                        principalTable: "ticket_messages",
                        principalColumns: new[] { "id", "ticket_id" });
                });

            migrationBuilder.CreateTable(
                name: "delinquency_cases",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    agreement_id = table.Column<long>(type: "bigint", nullable: false),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "grace"),
                    opened_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    grace_ends_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    outstanding_snapshot = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__delinque__3213E83F82280640", x => x.id);
                    table.CheckConstraint("CK_delinquency_cases_dates", "[grace_ends_at] >= [opened_at] AND ([resolved_at] IS NULL OR [resolved_at] >= [opened_at])");
                    table.CheckConstraint("CK_delinquency_cases_outstanding", "[outstanding_snapshot] > 0");
                    table.CheckConstraint("CK_delinquency_cases_status", "[status] IN ('grace', 'delinquent', 'cured', 'waived', 'termination_started')");
                    table.ForeignKey(
                        name: "FK__delinquen__agree__573DED66",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__delinquen__invoi__5832119F",
                        column: x => x.invoice_id,
                        principalSchema: "core",
                        principalTable: "invoices",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    line_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false, defaultValue: 1m),
                    unit_price = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    line_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true, computedColumnSql: "(CONVERT([numeric](14,2),round([quantity]*[unit_price],(2))))", stored: true),
                    metadata = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__invoice___3213E83F555DA62F", x => x.id);
                    table.CheckConstraint("CK_invoice_lines_line_type", "[line_type] IN ('deposit', 'rent', 'renewal', 'late_fee', 'damage', 'maintenance', 'booking_fee', 'discount', 'penalty', 'service', 'fee', 'tax', 'other')");
                    table.CheckConstraint("CK_invoice_lines_metadata_json", "ISJSON([metadata]) = 1");
                    table.CheckConstraint("CK_invoice_lines_quantity", "[quantity] > 0");
                    table.CheckConstraint("CK_invoice_lines_unit_price", "([line_type] = 'discount' AND [unit_price] <= 0) OR ([line_type] <> 'discount' AND [unit_price] >= 0)");
                    table.ForeignKey(
                        name: "FK__invoice_l__invoi__5E1FF51F",
                        column: x => x.invoice_id,
                        principalSchema: "core",
                        principalTable: "invoices",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    target_invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "VND"),
                    method = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    provider = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    provider_transaction_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    idempotency_key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "initiated"),
                    paid_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    failure_reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    metadata = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__payments__3213E83F1EE84ED7", x => x.id);
                    table.CheckConstraint("CK_payments_amount", "[amount] > 0");
                    table.CheckConstraint("CK_payments_currency_upper", "[currency] = upper([currency])");
                    table.CheckConstraint("CK_payments_metadata_json", "ISJSON([metadata]) = 1");
                    table.CheckConstraint("CK_payments_method", "[method] IN ('cash', 'bank_transfer', 'credit_card', 'vnpay', 'payos', 'stripe', 'other')");
                    table.CheckConstraint("CK_payments_paid_at", "([status] = 'succeeded' AND [paid_at] IS NOT NULL) OR [status] <> 'succeeded'");
                    table.CheckConstraint("CK_payments_status", "[status] IN ('initiated', 'pending', 'succeeded', 'failed', 'cancelled', 'partially_refunded', 'refunded')");
                    table.ForeignKey(
                        name: "FK__payments__custom__67A95F59",
                        column: x => x.customer_id,
                        principalSchema: "core",
                        principalTable: "customer_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__payments__target__689D8392",
                        column: x => x.target_invoice_id,
                        principalSchema: "core",
                        principalTable: "invoices",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "promotion_redemptions",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    promotion_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    reservation_id = table.Column<long>(type: "bigint", nullable: true),
                    invoice_id = table.Column<long>(type: "bigint", nullable: true),
                    discount_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "reserved"),
                    redeemed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__promotio__3213E83F4B06451A", x => x.id);
                    table.CheckConstraint("CK_promotion_redemptions_amount", "[discount_amount] >= 0");
                    table.CheckConstraint("CK_promotion_redemptions_status", "[status] IN ('reserved', 'applied', 'released')");
                    table.CheckConstraint("CK_promotion_redemptions_target", "(CASE WHEN [reservation_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [invoice_id] IS NULL THEN 0 ELSE 1 END) >= 1");
                    table.ForeignKey(
                        name: "FK__promotion__custo__0FB750B3",
                        column: x => x.customer_id,
                        principalSchema: "core",
                        principalTable: "customer_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__promotion__invoi__119F9925",
                        column: x => x.invoice_id,
                        principalSchema: "core",
                        principalTable: "invoices",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__promotion__promo__0EC32C7A",
                        column: x => x.promotion_id,
                        principalSchema: "core",
                        principalTable: "promotions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__promotion__reser__10AB74EC",
                        column: x => x.reservation_id,
                        principalSchema: "core",
                        principalTable: "reservations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "delinquency_actions",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    delinquency_case_id = table.Column<long>(type: "bigint", nullable: false),
                    action_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    performed_by = table.Column<long>(type: "bigint", nullable: true),
                    details = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__delinque__3213E83F0C91ECFE", x => x.id);
                    table.CheckConstraint("CK_delinquency_actions_details_json", "ISJSON([details]) = 1");
                    table.CheckConstraint("CK_delinquency_actions_type", "[action_type] IN ('reminder_sent', 'late_fee_added', 'credential_suspended', 'customer_contacted', 'payment_plan', 'waived', 'termination_started', 'note')");
                    table.ForeignKey(
                        name: "FK__delinquen__delin__60C757A0",
                        column: x => x.delinquency_case_id,
                        principalSchema: "core",
                        principalTable: "delinquency_cases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__delinquen__perfo__62AFA012",
                        column: x => x.performed_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                schema: "core",
                columns: table => new
                {
                    payment_id = table.Column<long>(type: "bigint", nullable: false),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    allocated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__payment___6247163E1D5CC991", x => new { x.payment_id, x.invoice_id });
                    table.CheckConstraint("CK_payment_allocations_amount", "[allocated_amount] > 0");
                    table.ForeignKey(
                        name: "FK__payment_a__invoi__76EBA2E9",
                        column: x => x.invoice_id,
                        principalSchema: "core",
                        principalTable: "invoices",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__payment_a__payme__75F77EB0",
                        column: x => x.payment_id,
                        principalSchema: "core",
                        principalTable: "payments",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payment_id = table.Column<long>(type: "bigint", nullable: false),
                    agreement_id = table.Column<long>(type: "bigint", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "VND"),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    provider = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    provider_refund_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    idempotency_key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "requested"),
                    requested_by = table.Column<long>(type: "bigint", nullable: true),
                    refunded_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__refunds__3213E83F6C90B5ED", x => x.id);
                    table.CheckConstraint("CK_refunds_amount", "[amount] > 0");
                    table.CheckConstraint("CK_refunds_currency_upper", "[currency] = upper([currency])");
                    table.CheckConstraint("CK_refunds_refunded_at", "([status] IN ('succeeded', 'completed') AND [refunded_at] IS NOT NULL) OR [status] NOT IN ('succeeded', 'completed')");
                    table.CheckConstraint("CK_refunds_status", "[status] IN ('requested', 'pending', 'approved', 'processing', 'succeeded', 'completed', 'failed', 'rejected', 'pending_manager_payout', 'pending_counter_payout')");
                    table.ForeignKey(
                        name: "FK__refunds__agreeme__7D98A078",
                        column: x => x.agreement_id,
                        principalSchema: "core",
                        principalTable: "rental_agreements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__refunds__payment__7CA47C3F",
                        column: x => x.payment_id,
                        principalSchema: "core",
                        principalTable: "payments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK__refunds__request__025D5595",
                        column: x => x.requested_by,
                        principalSchema: "core",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "refund_approvals",
                schema: "core",
                columns: table => new
                {
                    refund_id = table.Column<long>(type: "bigint", nullable: false),
                    decision = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    decided_by = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__refund_a__897E9EA3E281922A", x => x.refund_id);
                    table.CheckConstraint("CK_refund_approvals_decision", "[decision] IN ('approved', 'rejected')");
                    table.ForeignKey(
                        name: "FK__refund_ap__decid__0AF29B96",
                        column: x => x.decided_by,
                        principalSchema: "core",
                        principalTable: "employee_profiles",
                        principalColumn: "user_id");
                    table.ForeignKey(
                        name: "FK__refund_ap__refun__090A5324",
                        column: x => x.refund_id,
                        principalSchema: "core",
                        principalTable: "refunds",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "access_credentials_agreement_idx",
                schema: "core",
                table: "access_credentials",
                columns: new[] { "agreement_id", "status" });

            migrationBuilder.CreateIndex(
                name: "access_credentials_authorized_member_id_idx",
                schema: "core",
                table: "access_credentials",
                column: "authorized_member_id",
                filter: "([authorized_member_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "access_credentials_issued_by_idx",
                schema: "core",
                table: "access_credentials",
                column: "issued_by",
                filter: "([issued_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "access_events_credential_occurred_idx",
                schema: "core",
                table: "access_events",
                columns: new[] { "credential_id", "occurred_at" },
                descending: new[] { false, true },
                filter: "([credential_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "access_events_external_event_id_uidx",
                schema: "core",
                table: "access_events",
                column: "external_event_id",
                unique: true,
                filter: "([external_event_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "access_events_point_occurred_idx",
                schema: "core",
                table: "access_events",
                columns: new[] { "access_point_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "access_points_facility_id_idx",
                schema: "core",
                table: "access_points",
                columns: new[] { "facility_id", "status" });

            migrationBuilder.CreateIndex(
                name: "UQ__access_p__21BF3E60701E44D7",
                schema: "core",
                table: "access_points",
                columns: new[] { "facility_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "appointments_agreement_id_idx",
                schema: "core",
                table: "appointments",
                column: "agreement_id",
                filter: "([agreement_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "appointments_assigned_staff_id_idx",
                schema: "core",
                table: "appointments",
                column: "assigned_staff_id",
                filter: "([assigned_staff_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "appointments_facility_schedule_idx",
                schema: "core",
                table: "appointments",
                columns: new[] { "facility_id", "starts_at", "status" });

            migrationBuilder.CreateIndex(
                name: "appointments_reservation_id_idx",
                schema: "core",
                table: "appointments",
                column: "reservation_id",
                filter: "([reservation_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "audit_logs_actor_idx",
                schema: "core",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "occurred_at" },
                descending: new[] { false, true },
                filter: "([actor_user_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "audit_logs_entity_idx",
                schema: "core",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id", "occurred_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "audit_logs_request_id_idx",
                schema: "core",
                table: "audit_logs",
                column: "request_id",
                filter: "([request_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "authorized_access_members_agreement_idx",
                schema: "core",
                table: "authorized_access_members",
                columns: new[] { "agreement_id", "status" });

            migrationBuilder.CreateIndex(
                name: "authorized_access_members_created_by_idx",
                schema: "core",
                table: "authorized_access_members",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "customer_profiles_identity_number_uidx",
                schema: "core",
                table: "customer_profiles",
                column: "identity_number",
                unique: true,
                filter: "([identity_number] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "delinquency_actions_case_occurred_idx",
                schema: "core",
                table: "delinquency_actions",
                columns: new[] { "delinquency_case_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "delinquency_actions_performed_by_idx",
                schema: "core",
                table: "delinquency_actions",
                column: "performed_by",
                filter: "([performed_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "delinquency_cases_agreement_idx",
                schema: "core",
                table: "delinquency_cases",
                columns: new[] { "agreement_id", "status", "opened_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "delinquency_cases_invoice_id_idx",
                schema: "core",
                table: "delinquency_cases",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "delinquency_cases_one_open_invoice_uidx",
                schema: "core",
                table: "delinquency_cases",
                column: "invoice_id",
                unique: true,
                filter: "([resolved_at] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ__employee__B0AA73450E145A67",
                schema: "core",
                table: "employee_profiles",
                column: "employee_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "facilities_city_status_idx",
                schema: "core",
                table: "facilities",
                columns: new[] { "city", "status" });

            migrationBuilder.CreateIndex(
                name: "UQ__faciliti__357D4CF9081C918B",
                schema: "core",
                table: "facilities",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "facility_areas_parent_area_id_idx",
                schema: "core",
                table: "facility_areas",
                column: "parent_area_id",
                filter: "([parent_area_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_facility_areas_parent_area_id_facility_id",
                schema: "core",
                table: "facility_areas",
                columns: new[] { "parent_area_id", "facility_id" });

            migrationBuilder.CreateIndex(
                name: "UQ__facility__21BF3E60BE1B2B38",
                schema: "core",
                table: "facility_areas",
                columns: new[] { "facility_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__facility__C93D6694F87C9E30",
                schema: "core",
                table: "facility_areas",
                columns: new[] { "id", "facility_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "facility_rates_created_by_idx",
                schema: "core",
                table: "facility_rates",
                column: "created_by",
                filter: "([created_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "facility_rates_unit_type_id_idx",
                schema: "core",
                table: "facility_rates",
                column: "unit_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_facility_rates_facility_id",
                schema: "core",
                table: "facility_rates",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "fee_rules_created_by_idx",
                schema: "core",
                table: "fee_rules",
                column: "created_by",
                filter: "([created_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "fee_rules_facility_id_idx",
                schema: "core",
                table: "fee_rules",
                column: "facility_id",
                filter: "([facility_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "fee_rules_scope_code_start_uidx",
                schema: "core",
                table: "fee_rules",
                columns: new[] { "facility_id", "code", "valid_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "handover_records_agreement_idx",
                schema: "core",
                table: "handover_records",
                columns: new[] { "agreement_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "handover_records_handled_by_idx",
                schema: "core",
                table: "handover_records",
                column: "handled_by");

            migrationBuilder.CreateIndex(
                name: "handover_records_inspection_id_idx",
                schema: "core",
                table: "handover_records",
                column: "inspection_id");

            migrationBuilder.CreateIndex(
                name: "handover_records_unit_allocation_id_idx",
                schema: "core",
                table: "handover_records",
                column: "unit_allocation_id");

            migrationBuilder.CreateIndex(
                name: "identity_verifications_reservation_idx",
                schema: "core",
                table: "identity_verifications",
                columns: new[] { "reservation_id", "verified_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "identity_verifications_verified_by_idx",
                schema: "core",
                table: "identity_verifications",
                column: "verified_by");

            migrationBuilder.CreateIndex(
                name: "inspection_items_inspection_id_idx",
                schema: "core",
                table: "inspection_items",
                column: "inspection_id");

            migrationBuilder.CreateIndex(
                name: "inspections_agreement_id_idx",
                schema: "core",
                table: "inspections",
                column: "agreement_id",
                filter: "([agreement_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "inspections_facility_type_date_idx",
                schema: "core",
                table: "inspections",
                columns: new[] { "facility_id", "inspection_type", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "inspections_inspected_by_idx",
                schema: "core",
                table: "inspections",
                column: "inspected_by");

            migrationBuilder.CreateIndex(
                name: "inspections_reservation_id_idx",
                schema: "core",
                table: "inspections",
                column: "reservation_id",
                filter: "([reservation_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "inspections_storage_unit_id_idx",
                schema: "core",
                table: "inspections",
                column: "storage_unit_id",
                filter: "([storage_unit_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "integration_events_processing_queue_idx",
                schema: "core",
                table: "integration_events",
                columns: new[] { "received_at", "id" },
                filter: "([status] IN ('received', 'failed'))");

            migrationBuilder.CreateIndex(
                name: "UQ__integrat__86EB106AF963411C",
                schema: "core",
                table: "integration_events",
                columns: new[] { "source", "external_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "invoice_lines_invoice_id_idx",
                schema: "core",
                table: "invoice_lines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "invoices_agreement_billing_period_uidx",
                schema: "core",
                table: "invoices",
                columns: new[] { "agreement_id", "billing_period" },
                unique: true,
                filter: "([agreement_id] IS NOT NULL AND [billing_period] IS NOT NULL AND [status]<>'voided')");

            migrationBuilder.CreateIndex(
                name: "invoices_agreement_id_idx",
                schema: "core",
                table: "invoices",
                column: "agreement_id",
                filter: "([agreement_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "invoices_customer_status_due_idx",
                schema: "core",
                table: "invoices",
                columns: new[] { "customer_id", "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "invoices_open_due_idx",
                schema: "core",
                table: "invoices",
                column: "due_date",
                filter: "([status] IN ('open', 'partially_paid', 'overdue'))");

            migrationBuilder.CreateIndex(
                name: "invoices_reservation_id_idx",
                schema: "core",
                table: "invoices",
                column: "reservation_id",
                filter: "([reservation_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "invoices_ticket_charge_proposal_id_uidx",
                schema: "core",
                table: "invoices",
                column: "ticket_charge_proposal_id",
                unique: true,
                filter: "([ticket_charge_proposal_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_status_amounts",
                schema: "core",
                table: "invoices",
                columns: new[] { "status", "total_amount", "paid_amount" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_status_due_date",
                schema: "core",
                table: "invoices",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "UQ__invoices__F58CA1E2D09A819E",
                schema: "core",
                table: "invoices",
                column: "invoice_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "login_history_email_occurred_idx",
                schema: "core",
                table: "login_history",
                columns: new[] { "attempted_email", "occurred_at" },
                descending: new[] { false, true },
                filter: "([attempted_email] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "login_history_user_occurred_idx",
                schema: "core",
                table: "login_history",
                columns: new[] { "user_id", "occurred_at" },
                descending: new[] { false, true },
                filter: "([user_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "maintenance_work_orders_assigned_employee_id_idx",
                schema: "core",
                table: "maintenance_work_orders",
                column: "assigned_employee_id",
                filter: "([assigned_employee_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "maintenance_work_orders_facility_queue_idx",
                schema: "core",
                table: "maintenance_work_orders",
                columns: new[] { "facility_id", "status", "priority", "created_at" });

            migrationBuilder.CreateIndex(
                name: "maintenance_work_orders_opened_by_idx",
                schema: "core",
                table: "maintenance_work_orders",
                column: "opened_by");

            migrationBuilder.CreateIndex(
                name: "maintenance_work_orders_source_inspection_id_idx",
                schema: "core",
                table: "maintenance_work_orders",
                column: "source_inspection_id",
                filter: "([source_inspection_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "maintenance_work_orders_source_ticket_id_idx",
                schema: "core",
                table: "maintenance_work_orders",
                column: "source_ticket_id",
                filter: "([source_ticket_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "maintenance_work_orders_storage_unit_id_idx",
                schema: "core",
                table: "maintenance_work_orders",
                columns: new[] { "storage_unit_id", "status" },
                filter: "([storage_unit_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "maintenance_work_orders_verified_by_idx",
                schema: "core",
                table: "maintenance_work_orders",
                column: "verified_by",
                filter: "([verified_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ__maintena__58954FA67EC34FED",
                schema: "core",
                table: "maintenance_work_orders",
                column: "work_order_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "move_out_requests_agreement_idx",
                schema: "core",
                table: "move_out_requests",
                columns: new[] { "agreement_id", "status", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "move_out_requests_appointment_id_idx",
                schema: "core",
                table: "move_out_requests",
                column: "appointment_id",
                filter: "([appointment_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "move_out_requests_one_open_uidx",
                schema: "core",
                table: "move_out_requests",
                column: "agreement_id",
                unique: true,
                filter: "([status]<>'completed' AND [status]<>'cancelled')");

            migrationBuilder.CreateIndex(
                name: "move_out_requests_requested_by_idx",
                schema: "core",
                table: "move_out_requests",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "notifications_deduplication_key_uidx",
                schema: "core",
                table: "notifications",
                column: "deduplication_key",
                unique: true,
                filter: "([deduplication_key] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "notifications_delivery_queue_idx",
                schema: "core",
                table: "notifications",
                columns: new[] { "scheduled_at", "id" },
                filter: "([status] IN ('pending', 'failed'))");

            migrationBuilder.CreateIndex(
                name: "notifications_user_created_idx",
                schema: "core",
                table: "notifications",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "payment_allocations_invoice_id_idx",
                schema: "core",
                table: "payment_allocations",
                columns: new[] { "invoice_id", "payment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_customer_invoice_status",
                schema: "core",
                table: "payments",
                columns: new[] { "customer_id", "target_invoice_id", "status" });

            migrationBuilder.CreateIndex(
                name: "payments_customer_created_idx",
                schema: "core",
                table: "payments",
                columns: new[] { "customer_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "payments_provider_transaction_uidx",
                schema: "core",
                table: "payments",
                columns: new[] { "provider", "provider_transaction_id" },
                unique: true,
                filter: "([provider_transaction_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "payments_target_invoice_id_idx",
                schema: "core",
                table: "payments",
                column: "target_invoice_id");

            migrationBuilder.CreateIndex(
                name: "UQ__payments__A7BA59F488C60FE3",
                schema: "core",
                table: "payments",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "policy_versions_created_by_idx",
                schema: "core",
                table: "policy_versions",
                column: "created_by",
                filter: "([created_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ__policy_v__913F32FAF5C65EB9",
                schema: "core",
                table: "policy_versions",
                columns: new[] { "policy_type", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_ranges_unit_type_id",
                schema: "core",
                table: "price_ranges",
                column: "unit_type_id");

            migrationBuilder.CreateIndex(
                name: "price_ranges_created_by_idx",
                schema: "core",
                table: "price_ranges",
                column: "created_by",
                filter: "([created_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "promotion_redemptions_customer_idx",
                schema: "core",
                table: "promotion_redemptions",
                columns: new[] { "customer_id", "promotion_id", "status" });

            migrationBuilder.CreateIndex(
                name: "promotion_redemptions_invoice_id_idx",
                schema: "core",
                table: "promotion_redemptions",
                column: "invoice_id",
                filter: "([invoice_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "promotion_redemptions_one_per_invoice_uidx",
                schema: "core",
                table: "promotion_redemptions",
                column: "invoice_id",
                unique: true,
                filter: "([invoice_id] IS NOT NULL AND [status]<>'released')");

            migrationBuilder.CreateIndex(
                name: "promotion_redemptions_one_per_reservation_uidx",
                schema: "core",
                table: "promotion_redemptions",
                column: "reservation_id",
                unique: true,
                filter: "([reservation_id] IS NOT NULL AND [status]<>'released')");

            migrationBuilder.CreateIndex(
                name: "promotion_redemptions_promotion_idx",
                schema: "core",
                table: "promotion_redemptions",
                columns: new[] { "promotion_id", "status", "redeemed_at" });

            migrationBuilder.CreateIndex(
                name: "promotion_redemptions_reservation_id_idx",
                schema: "core",
                table: "promotion_redemptions",
                column: "reservation_id",
                filter: "([reservation_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "promotion_redemptions_reservation_uidx",
                schema: "core",
                table: "promotion_redemptions",
                columns: new[] { "promotion_id", "reservation_id" },
                unique: true,
                filter: "([reservation_id] IS NOT NULL AND [status]<>'released')");

            migrationBuilder.CreateIndex(
                name: "promotion_rules_promotion_id_idx",
                schema: "core",
                table: "promotion_rules",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "UQ__promotio__DE90F0E956A0A645",
                schema: "core",
                table: "promotion_rules",
                columns: new[] { "promotion_id", "rule_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "promotions_active_period_idx",
                schema: "core",
                table: "promotions",
                columns: new[] { "valid_from", "valid_to" },
                filter: "([is_active]=(1))");

            migrationBuilder.CreateIndex(
                name: "promotions_created_by_idx",
                schema: "core",
                table: "promotions",
                column: "created_by",
                filter: "([created_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ__promotio__357D4CF9D56E4DD4",
                schema: "core",
                table: "promotions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "refund_approvals_decided_by_idx",
                schema: "core",
                table: "refund_approvals",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "refunds_agreement_id_idx",
                schema: "core",
                table: "refunds",
                column: "agreement_id",
                filter: "([agreement_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "refunds_payment_id_idx",
                schema: "core",
                table: "refunds",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "refunds_provider_refund_uidx",
                schema: "core",
                table: "refunds",
                columns: new[] { "provider", "provider_refund_id" },
                unique: true,
                filter: "([provider_refund_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "refunds_requested_by_idx",
                schema: "core",
                table: "refunds",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "UQ__refunds__A7BA59F4B0FC8B69",
                schema: "core",
                table: "refunds",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "rental_agreements_customer_status_idx",
                schema: "core",
                table: "rental_agreements",
                columns: new[] { "customer_id", "status", "end_date" });

            migrationBuilder.CreateIndex(
                name: "rental_agreements_facility_status_idx",
                schema: "core",
                table: "rental_agreements",
                columns: new[] { "facility_id", "status", "end_date" });

            migrationBuilder.CreateIndex(
                name: "rental_agreements_policy_version_id_idx",
                schema: "core",
                table: "rental_agreements",
                column: "policy_version_id");

            migrationBuilder.CreateIndex(
                name: "UQ__rental_a__31384C28E1114D31",
                schema: "core",
                table: "rental_agreements",
                column: "reservation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__rental_a__A476927E4078718C",
                schema: "core",
                table: "rental_agreements",
                column: "agreement_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__rental_a__D1775C6C828F19B8",
                schema: "core",
                table: "rental_agreements",
                columns: new[] { "id", "customer_id", "facility_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "rental_renewals_agreement_idx",
                schema: "core",
                table: "rental_renewals",
                columns: new[] { "agreement_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "rental_renewals_one_open_uidx",
                schema: "core",
                table: "rental_renewals",
                column: "agreement_id",
                unique: true,
                filter: "([status] IN ('pending_payment', 'paid'))");

            migrationBuilder.CreateIndex(
                name: "rental_renewals_requested_by_idx",
                schema: "core",
                table: "rental_renewals",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "rental_renewals_reviewed_by_idx",
                schema: "core",
                table: "rental_renewals",
                column: "reviewed_by",
                filter: "([reviewed_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "reservations_customer_created_idx",
                schema: "core",
                table: "reservations",
                columns: new[] { "customer_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "reservations_facility_rate_id_idx",
                schema: "core",
                table: "reservations",
                column: "facility_rate_id");

            migrationBuilder.CreateIndex(
                name: "reservations_facility_status_start_idx",
                schema: "core",
                table: "reservations",
                columns: new[] { "facility_id", "status", "start_date" });

            migrationBuilder.CreateIndex(
                name: "reservations_open_hold_idx",
                schema: "core",
                table: "reservations",
                column: "hold_until",
                filter: "([status] IN ('pending', 'awaiting_deposit'))");

            migrationBuilder.CreateIndex(
                name: "reservations_unit_type_id_idx",
                schema: "core",
                table: "reservations",
                column: "unit_type_id");

            migrationBuilder.CreateIndex(
                name: "UQ__reservat__6DF00CF31E5AFD72",
                schema: "core",
                table: "reservations",
                columns: new[] { "id", "customer_id", "facility_id", "unit_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__reservat__FA8FADE4BD1ADEE8",
                schema: "core",
                table: "reservations",
                column: "reservation_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__roles__357D4CF96675988C",
                schema: "core",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "service_ratings_customer_id_idx",
                schema: "core",
                table: "service_ratings",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "shift_assignments_employee_id_idx",
                schema: "core",
                table: "shift_assignments",
                columns: new[] { "employee_id", "shift_id" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_facility_assignments_lookup",
                schema: "core",
                table: "staff_facility_assignments",
                columns: new[] { "employee_id", "facility_id", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "staff_facility_assignments_assigned_by_idx",
                schema: "core",
                table: "staff_facility_assignments",
                column: "assigned_by",
                filter: "([assigned_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "staff_facility_assignments_facility_idx",
                schema: "core",
                table: "staff_facility_assignments",
                columns: new[] { "facility_id", "assignment_role", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "staff_shifts_created_by_idx",
                schema: "core",
                table: "staff_shifts",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "staff_shifts_facility_schedule_idx",
                schema: "core",
                table: "staff_shifts",
                columns: new[] { "facility_id", "starts_at", "ends_at", "status" });

            migrationBuilder.CreateIndex(
                name: "staff_tasks_assigned_employee_id_idx",
                schema: "core",
                table: "staff_tasks",
                columns: new[] { "assigned_employee_id", "status", "due_at" },
                filter: "([assigned_employee_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "staff_tasks_created_by_idx",
                schema: "core",
                table: "staff_tasks",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "staff_tasks_facility_queue_idx",
                schema: "core",
                table: "staff_tasks",
                columns: new[] { "facility_id", "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "staff_tasks_maintenance_id_idx",
                schema: "core",
                table: "staff_tasks",
                column: "maintenance_work_order_id",
                filter: "([maintenance_work_order_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "staff_tasks_shift_id_idx",
                schema: "core",
                table: "staff_tasks",
                column: "shift_id",
                filter: "([shift_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "staff_tasks_ticket_id_idx",
                schema: "core",
                table: "staff_tasks",
                column: "ticket_id",
                filter: "([ticket_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "storage_units_area_id_idx",
                schema: "core",
                table: "storage_units",
                column: "area_id",
                filter: "([area_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "storage_units_catalog_idx",
                schema: "core",
                table: "storage_units",
                columns: new[] { "facility_id", "unit_type_id", "physical_status" },
                filter: "([is_listed]=(1))");

            migrationBuilder.CreateIndex(
                name: "storage_units_unit_type_id_idx",
                schema: "core",
                table: "storage_units",
                column: "unit_type_id");

            migrationBuilder.CreateIndex(
                name: "UQ__storage___01486F6FDC4326A5",
                schema: "core",
                table: "storage_units",
                columns: new[] { "id", "facility_id", "unit_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__storage___17930DB60694841D",
                schema: "core",
                table: "storage_units",
                columns: new[] { "facility_id", "unit_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__storage___C93D6694EC620F41",
                schema: "core",
                table: "storage_units",
                columns: new[] { "id", "facility_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "stored_items_agreement_id_idx",
                schema: "core",
                table: "stored_items",
                columns: new[] { "agreement_id", "category" });

            migrationBuilder.CreateIndex(
                name: "support_tickets_agreement_id_idx",
                schema: "core",
                table: "support_tickets",
                column: "agreement_id",
                filter: "([agreement_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "support_tickets_customer_created_idx",
                schema: "core",
                table: "support_tickets",
                columns: new[] { "customer_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "support_tickets_facility_queue_idx",
                schema: "core",
                table: "support_tickets",
                columns: new[] { "facility_id", "status", "priority", "created_at" });

            migrationBuilder.CreateIndex(
                name: "support_tickets_storage_unit_id_idx",
                schema: "core",
                table: "support_tickets",
                column: "storage_unit_id",
                filter: "([storage_unit_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ__support___D596C19626217F01",
                schema: "core",
                table: "support_tickets",
                column: "ticket_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ticket_assignments_assigned_by_idx",
                schema: "core",
                table: "ticket_assignments",
                column: "assigned_by",
                filter: "([assigned_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ticket_assignments_employee_id_idx",
                schema: "core",
                table: "ticket_assignments",
                columns: new[] { "employee_id", "ended_at" });

            migrationBuilder.CreateIndex(
                name: "ticket_assignments_one_active_uidx",
                schema: "core",
                table: "ticket_assignments",
                column: "ticket_id",
                unique: true,
                filter: "([ended_at] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ticket_assignments_ticket_history_idx",
                schema: "core",
                table: "ticket_assignments",
                columns: new[] { "ticket_id", "assigned_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_attachments_message_id_ticket_id",
                schema: "core",
                table: "ticket_attachments",
                columns: new[] { "message_id", "ticket_id" });

            migrationBuilder.CreateIndex(
                name: "ticket_attachments_message_id_idx",
                schema: "core",
                table: "ticket_attachments",
                column: "message_id",
                filter: "([message_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ticket_attachments_ticket_id_idx",
                schema: "core",
                table: "ticket_attachments",
                columns: new[] { "ticket_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ticket_attachments_uploaded_by_idx",
                schema: "core",
                table: "ticket_attachments",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ticket_charge_approvals_decided_by_idx",
                schema: "core",
                table: "ticket_charge_approvals",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ticket_charge_proposals_proposed_by_idx",
                schema: "core",
                table: "ticket_charge_proposals",
                column: "proposed_by");

            migrationBuilder.CreateIndex(
                name: "ticket_charge_proposals_ticket_idx",
                schema: "core",
                table: "ticket_charge_proposals",
                columns: new[] { "ticket_id", "status", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ticket_messages_author_user_id_idx",
                schema: "core",
                table: "ticket_messages",
                column: "author_user_id",
                filter: "([author_user_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ticket_messages_ticket_created_idx",
                schema: "core",
                table: "ticket_messages",
                columns: new[] { "ticket_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "UQ__ticket_m__9F4A87A8C907397C",
                schema: "core",
                table: "ticket_messages",
                columns: new[] { "id", "ticket_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "unit_allocations_agreement_period_idx",
                schema: "core",
                table: "unit_allocations",
                columns: new[] { "agreement_id", "allocation_start_date", "allocation_end_date" },
                filter: "([agreement_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "unit_allocations_assigned_by_idx",
                schema: "core",
                table: "unit_allocations",
                column: "assigned_by",
                filter: "([assigned_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "unit_allocations_availability_idx",
                schema: "core",
                table: "unit_allocations",
                columns: new[] { "storage_unit_id", "status", "allocation_start_date", "allocation_end_date" },
                filter: "([status]='active')");

            migrationBuilder.CreateIndex(
                name: "unit_allocations_one_active_reservation_uidx",
                schema: "core",
                table: "unit_allocations",
                column: "reservation_id",
                unique: true,
                filter: "([status]='active' AND [reservation_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "unit_allocations_unit_period_idx",
                schema: "core",
                table: "unit_allocations",
                columns: new[] { "storage_unit_id", "allocation_start_date", "allocation_end_date" });

            migrationBuilder.CreateIndex(
                name: "unit_map_positions_area_id_idx",
                schema: "core",
                table: "unit_map_positions",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "unit_status_history_changed_by_idx",
                schema: "core",
                table: "unit_status_history",
                column: "changed_by",
                filter: "([changed_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "unit_status_history_unit_changed_idx",
                schema: "core",
                table: "unit_status_history",
                columns: new[] { "storage_unit_id", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "unit_transfer_requests_agreement_idx",
                schema: "core",
                table: "unit_transfer_requests",
                columns: new[] { "agreement_id", "status", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "unit_transfer_requests_from_unit_id_idx",
                schema: "core",
                table: "unit_transfer_requests",
                column: "from_unit_id");

            migrationBuilder.CreateIndex(
                name: "unit_transfer_requests_one_open_uidx",
                schema: "core",
                table: "unit_transfer_requests",
                column: "agreement_id",
                unique: true,
                filter: "([status] IN ('pending_payment', 'paid', 'moved_out_in_transit', 'pending', 'approved', 'scheduled'))");

            migrationBuilder.CreateIndex(
                name: "unit_transfer_requests_requested_by_idx",
                schema: "core",
                table: "unit_transfer_requests",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "unit_transfer_requests_requested_facility_id_idx",
                schema: "core",
                table: "unit_transfer_requests",
                column: "requested_facility_id");

            migrationBuilder.CreateIndex(
                name: "unit_transfer_requests_requested_unit_type_id_idx",
                schema: "core",
                table: "unit_transfer_requests",
                column: "requested_unit_type_id");

            migrationBuilder.CreateIndex(
                name: "unit_transfer_requests_reviewed_by_idx",
                schema: "core",
                table: "unit_transfer_requests",
                column: "reviewed_by",
                filter: "([reviewed_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "unit_transfer_requests_target_invoice_id_idx",
                schema: "core",
                table: "unit_transfer_requests",
                column: "target_invoice_id",
                filter: "([target_invoice_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "unit_transfer_requests_to_unit_id_idx",
                schema: "core",
                table: "unit_transfer_requests",
                column: "to_unit_id",
                filter: "([to_unit_id] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ__unit_typ__357D4CF933DE2D48",
                schema: "core",
                table: "unit_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "user_roles_granted_by_idx",
                schema: "core",
                table: "user_roles",
                column: "granted_by",
                filter: "([granted_by] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "user_roles_role_id_idx",
                schema: "core",
                table: "user_roles",
                columns: new[] { "role_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "users_email_unique",
                schema: "core",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_events",
                schema: "core");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "core");

            migrationBuilder.DropTable(
                name: "delinquency_actions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "fee_rules",
                schema: "core");

            migrationBuilder.DropTable(
                name: "handover_records",
                schema: "core");

            migrationBuilder.DropTable(
                name: "identity_verifications",
                schema: "core");

            migrationBuilder.DropTable(
                name: "inspection_items",
                schema: "core");

            migrationBuilder.DropTable(
                name: "integration_events",
                schema: "core");

            migrationBuilder.DropTable(
                name: "invoice_lines",
                schema: "core");

            migrationBuilder.DropTable(
                name: "login_history",
                schema: "core");

            migrationBuilder.DropTable(
                name: "move_out_requests",
                schema: "core");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "core");

            migrationBuilder.DropTable(
                name: "payment_allocations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "price_ranges",
                schema: "core");

            migrationBuilder.DropTable(
                name: "promotion_redemptions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "promotion_rules",
                schema: "core");

            migrationBuilder.DropTable(
                name: "refund_approvals",
                schema: "core");

            migrationBuilder.DropTable(
                name: "rental_renewals",
                schema: "core");

            migrationBuilder.DropTable(
                name: "service_ratings",
                schema: "core");

            migrationBuilder.DropTable(
                name: "shift_assignments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "staff_facility_assignments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "staff_tasks",
                schema: "core");

            migrationBuilder.DropTable(
                name: "stored_items",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ticket_assignments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ticket_attachments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ticket_charge_approvals",
                schema: "core");

            migrationBuilder.DropTable(
                name: "unit_map_positions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "unit_status_history",
                schema: "core");

            migrationBuilder.DropTable(
                name: "unit_transfer_requests",
                schema: "core");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "access_points",
                schema: "core");

            migrationBuilder.DropTable(
                name: "access_credentials",
                schema: "core");

            migrationBuilder.DropTable(
                name: "delinquency_cases",
                schema: "core");

            migrationBuilder.DropTable(
                name: "unit_allocations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "appointments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "promotions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "refunds",
                schema: "core");

            migrationBuilder.DropTable(
                name: "maintenance_work_orders",
                schema: "core");

            migrationBuilder.DropTable(
                name: "staff_shifts",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ticket_messages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "authorized_access_members",
                schema: "core");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "inspections",
                schema: "core");

            migrationBuilder.DropTable(
                name: "invoices",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ticket_charge_proposals",
                schema: "core");

            migrationBuilder.DropTable(
                name: "employee_profiles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "support_tickets",
                schema: "core");

            migrationBuilder.DropTable(
                name: "rental_agreements",
                schema: "core");

            migrationBuilder.DropTable(
                name: "storage_units",
                schema: "core");

            migrationBuilder.DropTable(
                name: "policy_versions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "facility_areas",
                schema: "core");

            migrationBuilder.DropTable(
                name: "customer_profiles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "facility_rates",
                schema: "core");

            migrationBuilder.DropTable(
                name: "users",
                schema: "core");

            migrationBuilder.DropTable(
                name: "facilities",
                schema: "core");

            migrationBuilder.DropTable(
                name: "unit_types",
                schema: "core");

            migrationBuilder.DropSequence(
                name: "agreement_no_seq",
                schema: "core");

            migrationBuilder.DropSequence(
                name: "invoice_no_seq",
                schema: "core");

            migrationBuilder.DropSequence(
                name: "reservation_code_seq",
                schema: "core");

            migrationBuilder.DropSequence(
                name: "ticket_no_seq",
                schema: "core");

            migrationBuilder.DropSequence(
                name: "work_order_no_seq",
                schema: "core");
        }
    }
}
