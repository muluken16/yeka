using MySqlConnector;
using Dapper;
using CleaningManagmentSystem.Models;
using System.Text.Json;

namespace CleaningManagmentSystem.Data
{
    public static class DatabaseSeeder
    {
        private static readonly string[] RequiredTables = new[]
        {
            "users", "services", "bookings", "outsource_companies",
            "private_cleaning_companies", "private_company_receipts", "receipts",
            "payroll", "capital_transactions", "posts", "monthly_receipts",
            "meeting_rooms", "mahberat_reports", "dispatches", "office_plans",
            "library_items", "agency_reports", "yaka_reports", "subcity_officers",
            "subcity_drivers", "wereda_officers", "dispatch_schedules",
            "outsource_receipts", "office_recognitions", "trainings",
            "contact_messages", "user_settings", "gallery", "driver_locations",
            "delivery_tasks", "contacts", "messages", "checklists",
            "role_definitions", "role_permissions", "role_activity_logs",
            "system_usage_analytics", "weredas", "mahberats", "vehicles",
            "drivers", "staff_receipts", "transport_requests",
            "transport_request_logs", "transport_notifications",
            "requests", "request_approvals"
        };

        public static async Task SeedAsync(string connectionString)
        {
            Console.WriteLine("[Seeder] Starting database seeding...");
            await CreateTablesAsync(connectionString);
            await SeedRolesAndPermissionsAsync(connectionString);
            await SeedSampleUsersAsync(connectionString);
            await SeedSampleDataAsync(connectionString);
            await AddMissingColumnsAsync(connectionString);
            Console.WriteLine("[Seeder] Database seeding completed successfully!");
        }

        // ── Helper: check if column exists ──────────────────────────────────────
        private static async Task<bool> ColumnExistsAsync(MySqlConnection conn, string table, string column)
        {
            var cnt = await conn.QueryFirstOrDefaultAsync<int>(
                @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c",
                new { t = table, c = column });
            return cnt > 0;
        }

        // ── AddMissingColumns ───────────────────────────────────────────────────
        private static async Task AddMissingColumnsAsync(string connectionString)
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            try
            {
                // users: reset_token
                if (!await ColumnExistsAsync(connection, "users", "reset_token"))
                {
                    Console.WriteLine("[Seeder] Adding reset_token to users...");
                    await connection.ExecuteAsync("ALTER TABLE users ADD reset_token VARCHAR(255) NULL");
                }
                // users: reset_expires
                if (!await ColumnExistsAsync(connection, "users", "reset_expires"))
                {
                    Console.WriteLine("[Seeder] Adding reset_expires to users...");
                    await connection.ExecuteAsync("ALTER TABLE users ADD reset_expires DATETIME NULL");
                }

                // trainings: assigned_to_user_id
                if (!await ColumnExistsAsync(connection, "trainings", "assigned_to_user_id"))
                {
                    Console.WriteLine("[Seeder] Adding assigned_to_user_id to trainings...");
                    await connection.ExecuteAsync("ALTER TABLE trainings ADD assigned_to_user_id INT NULL");
                }
                // trainings: category
                if (!await ColumnExistsAsync(connection, "trainings", "category"))
                {
                    Console.WriteLine("[Seeder] Adding category to trainings...");
                    await connection.ExecuteAsync("ALTER TABLE trainings ADD category VARCHAR(100) DEFAULT 'General'");
                }

                // posts: training_id
                if (!await ColumnExistsAsync(connection, "posts", "training_id"))
                {
                    Console.WriteLine("[Seeder] Adding training_id to posts...");
                    await connection.ExecuteAsync("ALTER TABLE posts ADD training_id INT NULL");
                }
                // posts: is_pinned
                if (!await ColumnExistsAsync(connection, "posts", "is_pinned"))
                {
                    Console.WriteLine("[Seeder] Adding is_pinned to posts...");
                    await connection.ExecuteAsync("ALTER TABLE posts ADD is_pinned TINYINT DEFAULT 0");
                }
                // posts: priority
                if (!await ColumnExistsAsync(connection, "posts", "priority"))
                {
                    Console.WriteLine("[Seeder] Adding priority to posts...");
                    await connection.ExecuteAsync("ALTER TABLE posts ADD priority VARCHAR(50) DEFAULT 'Normal'");
                }
                // posts: target_role
                if (!await ColumnExistsAsync(connection, "posts", "target_role"))
                {
                    Console.WriteLine("[Seeder] Adding target_role to posts...");
                    await connection.ExecuteAsync("ALTER TABLE posts ADD target_role VARCHAR(100) DEFAULT 'All'");
                }

                // staff_receipts: notes, image_url, latitude, longitude
                if (!await ColumnExistsAsync(connection, "staff_receipts", "notes"))
                {
                    Console.WriteLine("[Seeder] Adding extra columns to staff_receipts...");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD notes TEXT NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD image_url VARCHAR(500) NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD latitude DECIMAL(10,8) NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD longitude DECIMAL(11,8) NULL");
                }

                // outsource_receipts: wereda_name check (recreate if missing)
                if (!await ColumnExistsAsync(connection, "outsource_receipts", "wereda_name"))
                {
                    Console.WriteLine("[Seeder] outsource_receipts has old schema. Recreating...");
                    await connection.ExecuteAsync("DROP TABLE IF EXISTS outsource_receipts");
                    await connection.ExecuteAsync(@"
                        CREATE TABLE outsource_receipts (
                            id INT AUTO_INCREMENT PRIMARY KEY,
                            wereda_id INT, wereda_name VARCHAR(255),
                            company_id INT, company_name VARCHAR(255),
                            vehicle_id INT, plate_number VARCHAR(50),
                            driver_id INT, driver_name VARCHAR(255),
                            receipt_time TIME, receipt_date DATE,
                            kilogram DECIMAL(10,2),
                            price DECIMAL(10,2) DEFAULT 0.00,
                            registered_by VARCHAR(255),
                            registered_at DATETIME DEFAULT NOW(),
                            status VARCHAR(50) DEFAULT 'Registered',
                            notes TEXT, image_url VARCHAR(500),
                            mahberat_approved TINYINT NULL,
                            mahberat_approved_by VARCHAR(255) NULL,
                            mahberat_approved_at DATETIME NULL,
                            mahberat_notes TEXT NULL,
                            approved_by VARCHAR(255) NULL,
                            approved_at DATETIME NULL,
                            rejected_by VARCHAR(255) NULL,
                            rejected_at DATETIME NULL,
                            reject_notes TEXT NULL
                        )");
                }

                // transport_requests: actual_kilogram
                if (!await ColumnExistsAsync(connection, "transport_requests", "actual_kilogram"))
                {
                    Console.WriteLine("[Seeder] Adding receipt fields to transport_requests...");
                    await connection.ExecuteAsync("ALTER TABLE transport_requests ADD actual_kilogram DECIMAL(10,2) NULL");
                    await connection.ExecuteAsync("ALTER TABLE transport_requests ADD receipt_wereda_id INT NULL");
                    await connection.ExecuteAsync("ALTER TABLE transport_requests ADD receipt_mahberat_id INT NULL");
                }

                // staff_receipts: mahberat_approved
                if (!await ColumnExistsAsync(connection, "staff_receipts", "mahberat_approved"))
                {
                    Console.WriteLine("[Seeder] Adding mahberat approval columns to staff_receipts...");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD mahberat_approved TINYINT NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD mahberat_approved_by VARCHAR(255) NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD mahberat_approved_at DATETIME NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD mahberat_notes TEXT NULL");
                }

                // outsource_receipts: mahberat_approved
                if (!await ColumnExistsAsync(connection, "outsource_receipts", "mahberat_approved"))
                {
                    Console.WriteLine("[Seeder] Adding mahberat approval columns to outsource_receipts...");
                    await connection.ExecuteAsync("ALTER TABLE outsource_receipts ADD mahberat_approved TINYINT NULL");
                    await connection.ExecuteAsync("ALTER TABLE outsource_receipts ADD mahberat_approved_by VARCHAR(255) NULL");
                    await connection.ExecuteAsync("ALTER TABLE outsource_receipts ADD mahberat_approved_at DATETIME NULL");
                    await connection.ExecuteAsync("ALTER TABLE outsource_receipts ADD mahberat_notes TEXT NULL");
                }

                // staff_receipts: approved_by
                if (!await ColumnExistsAsync(connection, "staff_receipts", "approved_by"))
                {
                    Console.WriteLine("[Seeder] Adding staff approval columns to staff_receipts...");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD approved_by VARCHAR(255) NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD approved_at DATETIME NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD rejected_by VARCHAR(255) NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD rejected_at DATETIME NULL");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD reject_notes TEXT NULL");
                }

                // outsource_receipts: approved_by
                if (!await ColumnExistsAsync(connection, "outsource_receipts", "approved_by"))
                {
                    Console.WriteLine("[Seeder] Adding staff approval columns to outsource_receipts...");
                    await connection.ExecuteAsync("ALTER TABLE outsource_receipts ADD approved_by VARCHAR(255) NULL");
                    await connection.ExecuteAsync("ALTER TABLE outsource_receipts ADD approved_at DATETIME NULL");
                    await connection.ExecuteAsync("ALTER TABLE outsource_receipts ADD rejected_by VARCHAR(255) NULL");
                    await connection.ExecuteAsync("ALTER TABLE outsource_receipts ADD rejected_at DATETIME NULL");
                    await connection.ExecuteAsync("ALTER TABLE outsource_receipts ADD reject_notes TEXT NULL");
                }

                // staff_receipts: transport_request_id
                if (!await ColumnExistsAsync(connection, "staff_receipts", "transport_request_id"))
                {
                    Console.WriteLine("[Seeder] Adding transport_request_id to staff_receipts...");
                    await connection.ExecuteAsync("ALTER TABLE staff_receipts ADD transport_request_id INT NULL");
                }

                // private_cleaning_companies: is_active
                if (!await ColumnExistsAsync(connection, "private_cleaning_companies", "is_active"))
                {
                    Console.WriteLine("[Seeder] Adding is_active to private_cleaning_companies...");
                    await connection.ExecuteAsync("ALTER TABLE private_cleaning_companies ADD is_active TINYINT DEFAULT 1");
                }

                // private_cleaning_companies: rep_user_id
                if (!await ColumnExistsAsync(connection, "private_cleaning_companies", "rep_user_id"))
                {
                    Console.WriteLine("[Seeder] Adding rep_user_id to private_cleaning_companies...");
                    await connection.ExecuteAsync("ALTER TABLE private_cleaning_companies ADD rep_user_id INT NULL");
                }

                // private_cleaning_companies: deleted_at
                if (!await ColumnExistsAsync(connection, "private_cleaning_companies", "deleted_at"))
                {
                    Console.WriteLine("[Seeder] Adding deleted_at to private_cleaning_companies...");
                    await connection.ExecuteAsync("ALTER TABLE private_cleaning_companies ADD deleted_at DATETIME NULL");
                }

                // Backfill rep_user_id
                try
                {
                    await connection.ExecuteAsync(@"
                        UPDATE private_cleaning_companies p
                        JOIN users u ON u.email = p.email AND u.role = 'PrivateCompanyRep' AND u.is_active = 1
                        SET p.rep_user_id = u.id
                        WHERE p.rep_user_id IS NULL AND p.email IS NOT NULL AND p.email <> ''");
                    Console.WriteLine("[Seeder] Backfilled rep_user_id on private_cleaning_companies.");
                }
                catch (Exception ex) { Console.WriteLine($"[Seeder] rep_user_id backfill warning: {ex.Message}"); }

                // Fix empty roles
                try
                {
                    var emptyRoleCount = await connection.QueryFirstOrDefaultAsync<int>(
                        "SELECT COUNT(*) FROM users WHERE role IS NULL OR role = ''");
                    if (emptyRoleCount > 0)
                    {
                        Console.WriteLine($"[Seeder] Found {emptyRoleCount} user(s) with empty role — setting to 'staff'.");
                        await connection.ExecuteAsync("UPDATE users SET role = 'staff' WHERE role IS NULL OR role = ''");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[Seeder] Empty role fix warning: {ex.Message}"); }

                // Backfill monthly_receipts
                try
                {
                    var unpopulated = await connection.QueryAsync<dynamic>(@"
                        SELECT tr.id, tr.request_number, tr.pickup_location, tr.destination,
                               tr.driver_name, tr.actual_kilogram, tr.transport_cost,
                               tr.paid_at, tr.staff_action_at, tr.updated_at, tr.created_at,
                               tr.mahberat_user_name, m.name AS mahberat_name
                        FROM transport_requests tr
                        LEFT JOIN mahberats m ON m.id = COALESCE(tr.receipt_mahberat_id, tr.mahberat_id)
                        WHERE tr.status IN ('Paid','StaffApproved','ReceiptVerified')
                          AND tr.request_number NOT IN (SELECT receipt_number FROM monthly_receipts)");

                    foreach (var tr in unpopulated)
                    {
                        DateTime completedAt = (DateTime)(tr.paid_at ?? tr.staff_action_at ?? tr.updated_at ?? tr.created_at);
                        decimal cost = (decimal)(tr.transport_cost ?? 0m);
                        decimal kg   = (decimal)(tr.actual_kilogram ?? 0m);
                        string  src  = $"Transport | {tr.pickup_location} → {tr.destination} | Mahberat: {tr.mahberat_name ?? tr.mahberat_user_name} | Driver: {tr.driver_name ?? "-"} | {kg} KG";

                        await connection.ExecuteAsync(@"
                            INSERT IGNORE INTO monthly_receipts
                              (receipt_number, month, year, total_amount, paid_amount, balance, status, source, created_at, updated_at)
                            VALUES (@Num, @Month, @Year, @Total, @Paid, 0, 'Billed', @Source, NOW(), NOW())",
                            new { Num = (string)tr.request_number, Month = completedAt.ToString("MMMM"),
                                  Year = completedAt.Year, Total = cost, Paid = cost, Source = src });
                    }

                    if (((IEnumerable<dynamic>)unpopulated).Any())
                        Console.WriteLine($"[Seeder] Backfilled {((IEnumerable<dynamic>)unpopulated).Count()} paid transport request(s) into monthly_receipts.");
                }
                catch (Exception ex) { Console.WriteLine($"[Seeder] monthly_receipts backfill warning: {ex.Message}"); }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Seeder] Column check error: {ex.Message}");
            }
        }

        // ── CreateTables ────────────────────────────────────────────────────────
        private static async Task CreateTablesAsync(string connectionString)
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            foreach (var table in RequiredTables)
            {
                try
                {
                    var exists = await connection.QueryFirstOrDefaultAsync<int>(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t",
                        new { t = table });

                    if (exists == 0)
                    {
                        Console.WriteLine($"[Seeder] Creating table: {table}");
                        string? sql = table switch
                        {
                            "users" => @"CREATE TABLE users (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                name VARCHAR(255) NOT NULL,
                                email VARCHAR(255) NOT NULL UNIQUE,
                                password VARCHAR(255) NOT NULL,
                                role VARCHAR(100) NOT NULL,
                                phone VARCHAR(50),
                                address TEXT,
                                is_active TINYINT(1) DEFAULT 1,
                                reset_token VARCHAR(255),
                                reset_expires DATETIME,
                                created_by INT,
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "services" => @"CREATE TABLE services (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                name VARCHAR(255) NOT NULL,
                                description TEXT,
                                price DECIMAL(10,2),
                                duration INT,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "bookings" => @"CREATE TABLE bookings (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                user_id INT, service_id INT,
                                booking_date DATE, booking_time TIME,
                                address TEXT, status VARCHAR(50),
                                notes TEXT, created_at DATETIME DEFAULT NOW()
                            )",
                            "outsource_companies" => @"CREATE TABLE outsource_companies (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                company_name VARCHAR(255) NOT NULL,
                                contact_person VARCHAR(255), phone VARCHAR(50),
                                email VARCHAR(255), license_number VARCHAR(100),
                                contract_start_date DATE, contract_end_date DATE,
                                status VARCHAR(50) DEFAULT 'Active',
                                services_provided TEXT,
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "private_cleaning_companies" => @"CREATE TABLE private_cleaning_companies (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                company_name VARCHAR(255) NOT NULL,
                                license_number VARCHAR(100),
                                contact_person VARCHAR(255), phone VARCHAR(50),
                                email VARCHAR(255), address TEXT,
                                services_offered TEXT,
                                status VARCHAR(50) DEFAULT 'Active',
                                is_active TINYINT DEFAULT 1,
                                rep_user_id INT NULL, deleted_at DATETIME NULL,
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "private_company_receipts" => @"CREATE TABLE private_company_receipts (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                company_id INT, company_name VARCHAR(255),
                                wereda_id INT, wereda_name VARCHAR(255),
                                vehicle_id INT, plate_number VARCHAR(50),
                                driver_id INT, driver_name VARCHAR(255),
                                receipt_time TIME, receipt_date DATE,
                                kilogram DECIMAL(10,2),
                                price DECIMAL(10,2) DEFAULT 0.00,
                                total_amount DECIMAL(10,2) DEFAULT 0.00,
                                notes TEXT, registered_by VARCHAR(255),
                                status VARCHAR(50) DEFAULT 'Registered',
                                registered_at DATETIME DEFAULT NOW()
                            )",
                            "receipts" => @"CREATE TABLE receipts (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                receipt_number VARCHAR(100) NOT NULL UNIQUE,
                                user_id INT, service_id INT,
                                client_name VARCHAR(255), description TEXT,
                                amount DECIMAL(10,2), payment_method VARCHAR(50),
                                receipt_date DATE, status VARCHAR(50) DEFAULT 'Pending',
                                notes TEXT, created_by INT,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "payroll" => @"CREATE TABLE payroll (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                employee_id INT NOT NULL, employee_name VARCHAR(255),
                                employee_role VARCHAR(100),
                                base_salary DECIMAL(10,2),
                                bonus DECIMAL(10,2) DEFAULT 0,
                                deductions DECIMAL(10,2) DEFAULT 0,
                                net_salary DECIMAL(10,2),
                                month VARCHAR(20), year INT,
                                status VARCHAR(50), payment_date DATE,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "capital_transactions" => @"CREATE TABLE capital_transactions (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                transaction_type VARCHAR(50),
                                description TEXT, amount DECIMAL(10,2),
                                balance DECIMAL(10,2), transaction_date DATE,
                                category VARCHAR(100), reference VARCHAR(255),
                                notes TEXT, created_by INT,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "posts" => @"CREATE TABLE posts (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                title VARCHAR(255), category VARCHAR(100),
                                content TEXT, training_id INT,
                                is_pinned TINYINT DEFAULT 0,
                                priority VARCHAR(50) DEFAULT 'Normal',
                                target_role VARCHAR(100) DEFAULT 'All',
                                author VARCHAR(255), author_id INT,
                                status VARCHAR(50), image_url VARCHAR(500),
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "monthly_receipts" => @"CREATE TABLE monthly_receipts (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                receipt_number VARCHAR(100) NOT NULL UNIQUE,
                                month VARCHAR(20), year INT,
                                total_amount DECIMAL(10,2), paid_amount DECIMAL(10,2),
                                balance DECIMAL(10,2), status VARCHAR(50),
                                source VARCHAR(255),
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "meeting_rooms" => @"CREATE TABLE meeting_rooms (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                room_name VARCHAR(255) NOT NULL, capacity INT,
                                location VARCHAR(255), equipment TEXT,
                                is_available TINYINT(1) DEFAULT 1,
                                status VARCHAR(50),
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "mahberat_reports" => @"CREATE TABLE mahberat_reports (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                report_number VARCHAR(100) NOT NULL UNIQUE,
                                title VARCHAR(255), description TEXT,
                                report_type VARCHAR(100), status VARCHAR(50),
                                file_path VARCHAR(500), generated_by INT,
                                generated_at DATETIME, created_at DATETIME DEFAULT NOW()
                            )",
                            "dispatches" => @"CREATE TABLE dispatches (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                dispatch_number VARCHAR(100) NOT NULL UNIQUE,
                                destination VARCHAR(255), origin VARCHAR(255),
                                driver_name VARCHAR(255), vehicle_number VARCHAR(100),
                                dispatch_date DATE, expected_arrival DATE,
                                status VARCHAR(50), contents TEXT,
                                priority VARCHAR(50), created_by INT,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "office_plans" => @"CREATE TABLE office_plans (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                plan_name VARCHAR(255), description TEXT,
                                floor VARCHAR(50), section VARCHAR(50),
                                layout_image VARCHAR(500),
                                effective_from DATE, effective_to DATE,
                                status VARCHAR(50), created_at DATETIME DEFAULT NOW()
                            )",
                            "library_items" => @"CREATE TABLE library_items (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                title VARCHAR(255), author VARCHAR(255),
                                category VARCHAR(100), isbn VARCHAR(50),
                                quantity INT DEFAULT 1, available INT DEFAULT 1,
                                location VARCHAR(255), added_date DATE,
                                status VARCHAR(50)
                            )",
                            "agency_reports" => @"CREATE TABLE agency_reports (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                report_number VARCHAR(100) NOT NULL UNIQUE,
                                agency_name VARCHAR(255), report_type VARCHAR(100),
                                period VARCHAR(50), summary TEXT,
                                file_path VARCHAR(500), generated_by INT,
                                generated_at DATETIME, status VARCHAR(50),
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "yaka_reports" => @"CREATE TABLE yaka_reports (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                report_number VARCHAR(100) NOT NULL UNIQUE,
                                title VARCHAR(255), category VARCHAR(100),
                                description TEXT, period VARCHAR(50),
                                file_path VARCHAR(500), generated_by INT,
                                generated_at DATETIME, created_at DATETIME DEFAULT NOW()
                            )",
                            "subcity_officers" => @"CREATE TABLE subcity_officers (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                name VARCHAR(255), email VARCHAR(255),
                                phone VARCHAR(50), subcity VARCHAR(255),
                                position VARCHAR(255), responsibilities TEXT,
                                is_active TINYINT(1) DEFAULT 1, created_at DATETIME DEFAULT NOW()
                            )",
                            "subcity_drivers" => @"CREATE TABLE subcity_drivers (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                name VARCHAR(255), license_number VARCHAR(100),
                                phone VARCHAR(50), subcity VARCHAR(255),
                                vehicle_assigned VARCHAR(255),
                                is_available TINYINT(1) DEFAULT 1, status VARCHAR(50),
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "wereda_officers" => @"CREATE TABLE wereda_officers (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                name VARCHAR(255), email VARCHAR(255),
                                phone VARCHAR(50), wereda VARCHAR(255),
                                position VARCHAR(255), responsibilities TEXT,
                                is_active TINYINT(1) DEFAULT 1, created_at DATETIME DEFAULT NOW()
                            )",
                            "dispatch_schedules" => @"CREATE TABLE dispatch_schedules (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                schedule_number VARCHAR(100) NOT NULL UNIQUE,
                                origin VARCHAR(255), destination VARCHAR(255),
                                scheduled_date DATE, scheduled_time TIME,
                                driver_name VARCHAR(255), vehicle_number VARCHAR(100),
                                purpose TEXT, status VARCHAR(50),
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "outsource_receipts" => @"CREATE TABLE outsource_receipts (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                wereda_id INT, wereda_name VARCHAR(255),
                                company_id INT, company_name VARCHAR(255),
                                vehicle_id INT, plate_number VARCHAR(50),
                                driver_id INT, driver_name VARCHAR(255),
                                receipt_time TIME, receipt_date DATE,
                                kilogram DECIMAL(10,2),
                                price DECIMAL(10,2) DEFAULT 0.00,
                                registered_by VARCHAR(255),
                                registered_at DATETIME DEFAULT NOW(),
                                status VARCHAR(50) DEFAULT 'Registered',
                                notes TEXT, image_url VARCHAR(500),
                                mahberat_approved TINYINT NULL,
                                mahberat_approved_by VARCHAR(255) NULL,
                                mahberat_approved_at DATETIME NULL,
                                mahberat_notes TEXT NULL,
                                approved_by VARCHAR(255) NULL,
                                approved_at DATETIME NULL,
                                rejected_by VARCHAR(255) NULL,
                                rejected_at DATETIME NULL,
                                reject_notes TEXT NULL
                            )",
                            "office_recognitions" => @"CREATE TABLE office_recognitions (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                title VARCHAR(255), recipient_name VARCHAR(255),
                                recipient_role VARCHAR(255), reason TEXT,
                                award_type VARCHAR(100), award_date DATE,
                                certificate_url VARCHAR(500), presented_by INT,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "trainings" => @"CREATE TABLE trainings (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                title VARCHAR(255), trainer VARCHAR(255),
                                description TEXT, start_date DATE, end_date DATE,
                                location VARCHAR(255), participants INT,
                                status VARCHAR(50), materials TEXT,
                                assigned_to_user_id INT NULL,
                                category VARCHAR(100) DEFAULT 'General',
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "contact_messages" => @"CREATE TABLE contact_messages (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                name VARCHAR(255), email VARCHAR(255),
                                subject VARCHAR(255), message TEXT,
                                phone VARCHAR(50), status VARCHAR(50) DEFAULT 'New',
                                created_at DATETIME DEFAULT NOW(),
                                replied_at DATETIME, reply TEXT
                            )",
                            "user_settings" => @"CREATE TABLE user_settings (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                user_id INT UNIQUE, setting_key VARCHAR(100),
                                setting_value TEXT, description TEXT,
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "gallery" => @"CREATE TABLE gallery (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                title VARCHAR(255), description TEXT,
                                image_url VARCHAR(500), category VARCHAR(100),
                                views INT DEFAULT 0, is_active TINYINT(1) DEFAULT 1,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "driver_locations" => @"CREATE TABLE driver_locations (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                driver_id INT NOT NULL, driver_name VARCHAR(255),
                                latitude DECIMAL(10,8), longitude DECIMAL(11,8),
                                address TEXT, notes TEXT,
                                updated_at DATETIME DEFAULT NOW(),
                                is_active TINYINT(1) DEFAULT 1,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "delivery_tasks" => @"CREATE TABLE delivery_tasks (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                task_number VARCHAR(100) NOT NULL UNIQUE,
                                driver_id INT, driver_name VARCHAR(255),
                                pickup_location TEXT, dropoff_location TEXT,
                                task_date DATE, pickup_time TIME,
                                status VARCHAR(50) DEFAULT 'Pending',
                                description TEXT, priority VARCHAR(50),
                                notes TEXT, assigned_by INT,
                                assigned_by_name VARCHAR(255),
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "contacts" => @"CREATE TABLE contacts (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                driver_id INT, name VARCHAR(255),
                                phone VARCHAR(50), email VARCHAR(255),
                                company VARCHAR(255), notes TEXT,
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "messages" => @"CREATE TABLE messages (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                sender_id INT, recipient_phone VARCHAR(50),
                                content TEXT,
                                sent_at DATETIME DEFAULT NOW(),
                                status VARCHAR(50) DEFAULT 'Sent'
                            )",
                            "checklists" => @"CREATE TABLE checklists (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                title VARCHAR(255), description TEXT,
                                assigned_to VARCHAR(255), assigned_to_user_id INT,
                                category VARCHAR(100), priority VARCHAR(50),
                                status VARCHAR(50) DEFAULT 'Pending',
                                due_date DATE, completed_date DATE,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "role_definitions" => @"CREATE TABLE role_definitions (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                role_name VARCHAR(100) NOT NULL UNIQUE,
                                display_name VARCHAR(255), description TEXT,
                                usage_context TEXT,
                                primary_responsibilities TEXT,
                                daily_activities TEXT,
                                reports_access TEXT,
                                modules_access TEXT,
                                access_level INT DEFAULT 1,
                                can_create_users TINYINT(1) DEFAULT 0,
                                can_view_financials TINYINT(1) DEFAULT 0,
                                can_manage_dispatch TINYINT(1) DEFAULT 0,
                                can_view_payroll TINYINT(1) DEFAULT 0,
                                can_manage_staff TINYINT(1) DEFAULT 0,
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "role_permissions" => @"CREATE TABLE role_permissions (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                role_name VARCHAR(100) NOT NULL,
                                module_name VARCHAR(255) NOT NULL,
                                permission_type VARCHAR(50) NOT NULL,
                                is_allowed TINYINT(1) DEFAULT 1,
                                description TEXT,
                                created_at DATETIME DEFAULT NOW(),
                                CONSTRAINT uq_role_perm UNIQUE (role_name, module_name, permission_type)
                            )",
                            "role_activity_logs" => @"CREATE TABLE role_activity_logs (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                user_id INT, user_name VARCHAR(255),
                                user_role VARCHAR(100), activity_type VARCHAR(100),
                                page_accessed VARCHAR(255),
                                action_performed VARCHAR(255),
                                details TEXT,
                                timestamp DATETIME DEFAULT NOW()
                            )",
                            "system_usage_analytics" => @"CREATE TABLE system_usage_analytics (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                role_name VARCHAR(100), metric_name VARCHAR(255),
                                metric_value INT, period VARCHAR(50),
                                record_date DATE, notes TEXT,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "weredas" => @"CREATE TABLE weredas (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                name VARCHAR(255) NOT NULL, description TEXT,
                                subcity VARCHAR(255), is_active TINYINT(1) DEFAULT 1,
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "mahberats" => @"CREATE TABLE mahberats (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                name VARCHAR(255) NOT NULL, wereda_id INT,
                                wereda_name VARCHAR(255), contact_person VARCHAR(255),
                                phone VARCHAR(50), email VARCHAR(255),
                                address TEXT, is_active TINYINT(1) DEFAULT 1,
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "vehicles" => @"CREATE TABLE vehicles (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                plate_number VARCHAR(50) NOT NULL UNIQUE,
                                vehicle_type VARCHAR(100), model VARCHAR(255),
                                color VARCHAR(100), driver_id INT,
                                driver_name VARCHAR(255),
                                status VARCHAR(50) DEFAULT 'Available',
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "drivers" => @"CREATE TABLE drivers (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                full_name VARCHAR(255) NOT NULL,
                                phone VARCHAR(50), email VARCHAR(255),
                                license_number VARCHAR(100) UNIQUE,
                                license_type VARCHAR(100), license_expiry DATE,
                                address TEXT, is_active TINYINT(1) DEFAULT 1,
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "staff_receipts" => @"CREATE TABLE staff_receipts (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                wereda_id INT, wereda_name VARCHAR(255),
                                mahberat_id INT, mahberat_name VARCHAR(255),
                                vehicle_id INT, plate_number VARCHAR(50),
                                driver_id INT, driver_name VARCHAR(255),
                                receipt_time TIME, receipt_date DATE,
                                kilogram DECIMAL(10,2),
                                price DECIMAL(10,2) DEFAULT 0.00,
                                registered_by VARCHAR(255),
                                registered_at DATETIME DEFAULT NOW(),
                                status VARCHAR(50) DEFAULT 'Registered',
                                notes TEXT, image_url VARCHAR(500),
                                latitude DECIMAL(10,8), longitude DECIMAL(11,8),
                                mahberat_approved TINYINT NULL,
                                mahberat_approved_by VARCHAR(255) NULL,
                                mahberat_approved_at DATETIME NULL,
                                mahberat_notes TEXT NULL,
                                approved_by VARCHAR(255) NULL,
                                approved_at DATETIME NULL,
                                rejected_by VARCHAR(255) NULL,
                                rejected_at DATETIME NULL,
                                reject_notes TEXT NULL,
                                transport_request_id INT NULL
                            )",
                            "transport_requests" => @"CREATE TABLE transport_requests (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                request_number VARCHAR(50) NOT NULL UNIQUE,
                                mahberat_user_id INT NOT NULL,
                                mahberat_user_name VARCHAR(255) NOT NULL,
                                mahberat_id INT, mahberat_name VARCHAR(255),
                                pickup_location TEXT NOT NULL,
                                destination TEXT NOT NULL,
                                passenger_item_details TEXT,
                                requested_date DATE NOT NULL,
                                requested_time VARCHAR(10),
                                special_instructions TEXT,
                                dispatcher_id INT, dispatcher_name VARCHAR(255),
                                dispatcher_notes TEXT,
                                dispatcher_action_at DATETIME,
                                driver_id INT, driver_name VARCHAR(255),
                                vehicle_id INT, vehicle_plate VARCHAR(50),
                                driver_notes TEXT,
                                driver_action_at DATETIME,
                                pickup_confirmed_at DATETIME,
                                pickup_notes TEXT,
                                mahberat_pickup_approved_at DATETIME,
                                mahberat_pickup_notes TEXT,
                                receipt_photo_url VARCHAR(500),
                                digital_receipt_url VARCHAR(500),
                                receipt_notes TEXT,
                                receipt_submitted_at DATETIME,
                                mahberat_verified_at DATETIME,
                                mahberat_verification_notes TEXT,
                                staff_id INT, staff_name VARCHAR(255),
                                transport_cost DECIMAL(10,2),
                                staff_notes TEXT,
                                staff_action_at DATETIME,
                                finance_staff_id INT, finance_staff_name VARCHAR(255),
                                transaction_number VARCHAR(100),
                                payment_proof_url VARCHAR(500),
                                paid_at DATETIME, payment_notes TEXT,
                                actual_kilogram DECIMAL(10,2) NULL,
                                receipt_wereda_id INT NULL,
                                receipt_mahberat_id INT NULL,
                                status VARCHAR(50) NOT NULL DEFAULT 'PendingDispatcher',
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "transport_request_logs" => @"CREATE TABLE transport_request_logs (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                transport_request_id INT NOT NULL,
                                from_status VARCHAR(50), to_status VARCHAR(50) NOT NULL,
                                actor_user_id INT NOT NULL, actor_name VARCHAR(255),
                                actor_role VARCHAR(100), notes TEXT,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "transport_notifications" => @"CREATE TABLE transport_notifications (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                recipient_user_id INT NOT NULL,
                                transport_request_id INT NOT NULL,
                                request_number VARCHAR(50),
                                title VARCHAR(255) NOT NULL,
                                body TEXT,
                                notification_type VARCHAR(50) DEFAULT 'Info',
                                is_read TINYINT DEFAULT 0,
                                created_at DATETIME DEFAULT NOW()
                            )",
                            "requests" => @"CREATE TABLE requests (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                requestor_id INT NOT NULL,
                                requestor_role VARCHAR(50),
                                wereda_id INT, mahberat_id INT,
                                request_type VARCHAR(100) NOT NULL,
                                description TEXT,
                                quantity DECIMAL(10,2),
                                urgency VARCHAR(20) DEFAULT 'Medium',
                                requested_date_time DATETIME,
                                attachment_path VARCHAR(255),
                                assigned_to_id INT,
                                execution_status VARCHAR(50) DEFAULT 'Pending',
                                completion_date DATETIME,
                                execution_report_path VARCHAR(255),
                                is_closed TINYINT(1) DEFAULT 0,
                                closed_by_id INT, closure_date DATETIME,
                                closure_remarks TEXT,
                                created_at DATETIME DEFAULT NOW(),
                                updated_at DATETIME DEFAULT NOW()
                            )",
                            "request_approvals" => @"CREATE TABLE request_approvals (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                request_id INT NOT NULL,
                                level_1_status VARCHAR(20) DEFAULT 'Pending',
                                level_1_by INT, level_1_date DATETIME,
                                level_1_comments TEXT,
                                level_2_status VARCHAR(20) DEFAULT 'Pending',
                                level_2_by INT, level_2_date DATETIME,
                                level_2_comments TEXT,
                                level_3_status VARCHAR(20) DEFAULT 'Pending',
                                level_3_by INT, level_3_date DATETIME,
                                level_3_comments TEXT
                            )",
                            _ => null
                        };

                        if (sql != null)
                            await connection.ExecuteAsync(sql);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Seeder] Error creating table {table}: {ex.Message}");
                }
            }
        }

        // ── SeedRolesAndPermissions ─────────────────────────────────────────────
        private static async Task SeedRolesAndPermissionsAsync(string connectionString)
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var roleDefinitions = new[]
            {
                new { RoleName="superadmin", DisplayName="Super Administrator",
                    Description="Highest level access with complete system control.",
                    UsageContext="Top-level administrative tasks across the entire system.",
                    PrimaryResponsibilities="Manage all users, configure settings, oversee financials.",
                    DailyActivities="Review logs, approve users, monitor transactions.",
                    ReportsAccess="All Reports", ModulesAccess="All Modules",
                    AccessLevel=4, CanCreateUsers=true, CanViewFinancials=true,
                    CanManageDispatch=true, CanViewPayroll=true, CanManageStaff=true },
                new { RoleName="manager", DisplayName="Manager",
                    Description="Middle management overseeing operations.",
                    UsageContext="Coordinate between staff and clients.",
                    PrimaryResponsibilities="Manage staff, approve bookings, monitor quality.",
                    DailyActivities="Review schedule, assign tasks, check bookings.",
                    ReportsAccess="Booking, Staff Performance, Revenue",
                    ModulesAccess="Staff, Bookings, Services, Reports",
                    AccessLevel=3, CanCreateUsers=false, CanViewFinancials=true,
                    CanManageDispatch=false, CanViewPayroll=false, CanManageStaff=true },
                new { RoleName="staff", DisplayName="Staff Member",
                    Description="Operational staff performing services.",
                    UsageContext="Execute daily operations and maintain records.",
                    PrimaryResponsibilities="Perform services, maintain checklists, submit reports.",
                    DailyActivities="View tasks, mark complete, update records.",
                    ReportsAccess="Personal Tasks, Agency Reports",
                    ModulesAccess="Services, Checklists, Reports, Dispatch, Receipts, Training",
                    AccessLevel=2, CanCreateUsers=false, CanViewFinancials=false,
                    CanManageDispatch=false, CanViewPayroll=false, CanManageStaff=false },
                new { RoleName="cleaner", DisplayName="Cleaner",
                    Description="Field personnel executing cleaning tasks.",
                    UsageContext="On-site cleaning technicians.",
                    PrimaryResponsibilities="Perform cleaning, follow safety procedures.",
                    DailyActivities="View assignments, log hours, report issues.",
                    ReportsAccess="Personal Job History",
                    ModulesAccess="My Jobs, Schedule, Profile",
                    AccessLevel=1, CanCreateUsers=false, CanViewFinancials=false,
                    CanManageDispatch=false, CanViewPayroll=false, CanManageStaff=false },
                new { RoleName="user", DisplayName="Registered User",
                    Description="Customer who books cleaning services.",
                    UsageContext="Clients booking for homes or businesses.",
                    PrimaryResponsibilities="Browse services, book appointments.",
                    DailyActivities="View services, check appointments.",
                    ReportsAccess="Personal Booking History",
                    ModulesAccess="Services, Bookings, Profile, Gallery, Contact",
                    AccessLevel=1, CanCreateUsers=false, CanViewFinancials=false,
                    CanManageDispatch=false, CanViewPayroll=false, CanManageStaff=false },
                new { RoleName="wereda_mahberat", DisplayName="Wereda Mahberat Officer",
                    Description="Wereda-level officer managing receipts and payroll.",
                    UsageContext="Government officers managing local financial records.",
                    PrimaryResponsibilities="Record receipts, process payroll, maintain capital records.",
                    DailyActivities="Enter receipts, review payroll, update capital ledger.",
                    ReportsAccess="Monthly Receipts, Payroll, Capital Statements",
                    ModulesAccess="Monthly Receipt, Payroll, Capital, Gallery, Contact",
                    AccessLevel=2, CanCreateUsers=false, CanViewFinancials=true,
                    CanManageDispatch=false, CanViewPayroll=true, CanManageStaff=false },
                new { RoleName="dispatch_officer", DisplayName="Dispatch Officer",
                    Description="Logistics coordinator managing dispatch and meeting rooms.",
                    UsageContext="Dispatch team scheduling deliveries and meetings.",
                    PrimaryResponsibilities="Schedule rooms, create dispatches, generate reports.",
                    DailyActivities="Check schedule, create dispatches, assign drivers.",
                    ReportsAccess="Dispatch Reports, Meeting Room, Mahberat Reports",
                    ModulesAccess="Meeting Room, Dispatch, Reports, Gallery, Contact",
                    AccessLevel=3, CanCreateUsers=false, CanViewFinancials=false,
                    CanManageDispatch=true, CanViewPayroll=false, CanManageStaff=false },
                new { RoleName="driver", DisplayName="Driver",
                    Description="Vehicle operator following dispatch instructions.",
                    UsageContext="Drivers carrying out delivery assignments.",
                    PrimaryResponsibilities="Follow dispatch, complete deliveries, update status.",
                    DailyActivities="View tasks, update status, report location.",
                    ReportsAccess="Personal Task History, Delivery Performance",
                    ModulesAccess="Dashboard, Location Tracking, Task Management, Contact",
                    AccessLevel=1, CanCreateUsers=false, CanViewFinancials=false,
                    CanManageDispatch=false, CanViewPayroll=false, CanManageStaff=false }
            };

            foreach (var role in roleDefinitions)
            {
                var existing = await connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT id FROM role_definitions WHERE role_name = @RoleName", new { role.RoleName });

                if (existing == null)
                {
                    Console.WriteLine($"[Seeder] Inserting role: {role.RoleName}");
                    await connection.ExecuteAsync(@"
                        INSERT INTO role_definitions
                          (role_name, display_name, description, usage_context,
                           primary_responsibilities, daily_activities, reports_access,
                           modules_access, access_level, can_create_users,
                           can_view_financials, can_manage_dispatch,
                           can_view_payroll, can_manage_staff, created_at)
                        VALUES
                          (@RoleName, @DisplayName, @Description, @UsageContext,
                           @PrimaryResponsibilities, @DailyActivities, @ReportsAccess,
                           @ModulesAccess, @AccessLevel, @CanCreateUsers,
                           @CanViewFinancials, @CanManageDispatch,
                           @CanViewPayroll, @CanManageStaff, NOW())", role);
                }
            }
        }

        // ── SeedSampleUsers ─────────────────────────────────────────────────────
        private static async Task SeedSampleUsersAsync(string connectionString)
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var sampleUsers = new[]
            {
                new { Name="Super Admin",          Email="superadmin@yeka.et",   Password="admin123",     Role="superadmin",       Phone="+251911234567" },
                new { Name="Operations Manager",   Email="manager@yeka.et",      Password="manager123",   Role="manager",          Phone="+251911234568" },
                new { Name="Staff Member A",        Email="staff@yeka.et",        Password="staff123",     Role="staff",            Phone="+251911234569" },
                new { Name="Cleaner B",             Email="cleaner@yeka.et",      Password="clean123",     Role="cleaner",          Phone="+251911234570" },
                new { Name="Customer X",            Email="user@client.et",       Password="user123",      Role="user",             Phone="+251911234571" },
                new { Name="Wereda Officer - Addis",Email="wereda@addis.gov.et",  Password="wereda123",    Role="wereda_mahberat",  Phone="+251911234572" },
                new { Name="Dispatch Lead",         Email="dispatch@yeka.et",     Password="dispatch123",  Role="dispatch_officer", Phone="+251911234573" },
                new { Name="Driver - Vehicle 01",   Email="driver1@yeka.et",      Password="driver123",    Role="driver",           Phone="+251911234574" },
                new { Name="Test Driver",           Email="driver@yeka.com",      Password="driver123",    Role="driver",           Phone="+251911234575" },
                new { Name="Test Outsource",        Email="outsource@yeka.com",   Password="outsource123", Role="outsource",        Phone="+251911234576" },
                new { Name="Outsource Rep",         Email="outsource@yeka.et",    Password="outsource123", Role="outsource",        Phone="+251911234581" },
                new { Name="Private Company Rep",   Email="private@yeka.et",      Password="private123",   Role="PrivateCompanyRep",Phone="+251911234582" },
                new { Name="Mahberat User - Bole",  Email="mahberat@yeka.et",     Password="mahberat123",  Role="WeredaMahberat",   Phone="+251911234577" },
                new { Name="Dispatcher - Transport",Email="dispatcher@yeka.et",   Password="dispatch123",  Role="DispatchOfficer",  Phone="+251911234578" },
                new { Name="Driver - Transport 01", Email="tdriver1@yeka.et",     Password="driver123",    Role="Driver",           Phone="+251911234579" },
                new { Name="Finance Staff",         Email="finance@yeka.et",      Password="finance123",   Role="Staff",            Phone="+251911234580" }
            };

            foreach (var user in sampleUsers)
            {
                var existing = await connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT id FROM users WHERE email = @Email", new { user.Email });

                if (existing == null)
                {
                    Console.WriteLine($"[Seeder] Creating user: {user.Name} ({user.Role})");
                    await connection.ExecuteAsync(@"
                        INSERT INTO users (name, email, password, role, phone, is_active, created_at)
                        VALUES (@Name, @Email, @Password, @Role, @Phone, 1, NOW())", user);
                }
            }

            // Link private@yeka.et to a company profile
            try
            {
                var privUser = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT id FROM users WHERE email = 'private@yeka.et'");
                if (privUser != null)
                {
                    int privId = (int)privUser.id;
                    var privCompany = await connection.QueryFirstOrDefaultAsync<int?>(
                        "SELECT id FROM private_cleaning_companies WHERE rep_user_id = @Id", new { Id = privId });
                    if (privCompany == null)
                    {
                        Console.WriteLine("[Seeder] Linking private@yeka.et to sample private company...");
                        await connection.ExecuteAsync(@"
                            INSERT INTO private_cleaning_companies
                              (company_name, license_number, contact_person, phone, email,
                               address, services_offered, status, is_active, rep_user_id, created_at, updated_at)
                            VALUES
                              ('Sample Private Cleaners','LIC-SAMPLE-01','Private Company Rep',
                               '+251911234582','private@yeka.et',
                               'Yeka Sub-city, Addis Ababa','Waste collection, Street sweeping',
                               'Active',1,@RepId,NOW(),NOW())",
                            new { RepId = privId });
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Seeder] Private company seed warning: {ex.Message}"); }

            // Auto-create companies for unlinked PrivateCompanyRep users
            try
            {
                var unlinkedReps = await connection.QueryAsync<dynamic>(@"
                    SELECT u.id, u.name, u.email, u.phone
                    FROM users u
                    WHERE u.role = 'PrivateCompanyRep' AND u.is_active = 1
                      AND NOT EXISTS (
                          SELECT 1 FROM private_cleaning_companies p
                          WHERE p.rep_user_id = u.id OR p.email = u.email
                      )");

                foreach (var rep in unlinkedReps)
                {
                    Console.WriteLine($"[Seeder] Auto-creating company for: {rep.name}");
                    await connection.ExecuteAsync(@"
                        INSERT INTO private_cleaning_companies
                          (company_name, contact_person, phone, email,
                           license_number, address, services_offered,
                           status, is_active, rep_user_id, created_at, updated_at)
                        VALUES (@cn,@cp,@ph,@em,'','','','Active',1,@uid,NOW(),NOW())",
                        new { cn=(string)rep.name+"'s Company", cp=(string)rep.name,
                              ph=(string)(rep.phone??""), em=(string)rep.email, uid=(int)rep.id });
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Seeder] Auto-create company warning: {ex.Message}"); }
        }

        // ── SeedSampleData ──────────────────────────────────────────────────────
        private static async Task SeedSampleDataAsync(string connectionString)
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var permissions = new[]
            {
                new { RoleName="superadmin",       ModuleName="users",            PermissionType="CRUD",   Description="Full user management",          IsAllowed=true },
                new { RoleName="superadmin",       ModuleName="receipts",         PermissionType="CRUD",   Description="All receipt operations",        IsAllowed=true },
                new { RoleName="superadmin",       ModuleName="payroll",          PermissionType="CRUD",   Description="Payroll management",            IsAllowed=true },
                new { RoleName="superadmin",       ModuleName="capital",          PermissionType="CRUD",   Description="Capital transactions",          IsAllowed=true },
                new { RoleName="superadmin",       ModuleName="reports",          PermissionType="View",   Description="All reports",                   IsAllowed=true },
                new { RoleName="superadmin",       ModuleName="posts",            PermissionType="CRUD",   Description="Post management",               IsAllowed=true },
                new { RoleName="wereda_mahberat",  ModuleName="monthly_receipts", PermissionType="CRUD",   Description="Monthly receipt management",    IsAllowed=true },
                new { RoleName="wereda_mahberat",  ModuleName="payroll",          PermissionType="View",   Description="View payroll",                  IsAllowed=true },
                new { RoleName="wereda_mahberat",  ModuleName="capital",          PermissionType="CRUD",   Description="Capital records",               IsAllowed=true },
                new { RoleName="wereda_mahberat",  ModuleName="reports",          PermissionType="View",   Description="View mahberat reports",         IsAllowed=true },
                new { RoleName="dispatch_officer", ModuleName="meeting_rooms",    PermissionType="CRUD",   Description="Meeting room management",       IsAllowed=true },
                new { RoleName="dispatch_officer", ModuleName="dispatches",       PermissionType="CRUD",   Description="Create and manage dispatches",  IsAllowed=true },
                new { RoleName="dispatch_officer", ModuleName="reports",          PermissionType="View",   Description="View all mahberat reports",     IsAllowed=true },
                new { RoleName="staff",            ModuleName="checklist",        PermissionType="CRUD",   Description="Manage personal checklist",     IsAllowed=true },
                new { RoleName="staff",            ModuleName="reports",          PermissionType="Create", Description="Create agency and yaka reports", IsAllowed=true }
            };

            foreach (var perm in permissions)
            {
                var existing = await connection.QueryFirstOrDefaultAsync<int?>(
                    @"SELECT id FROM role_permissions
                      WHERE role_name=@RoleName AND module_name=@ModuleName AND permission_type=@PermissionType",
                    perm);
                if (existing == null)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO role_permissions
                          (role_name,module_name,permission_type,description,is_allowed,created_at)
                        VALUES (@RoleName,@ModuleName,@PermissionType,@Description,@IsAllowed,NOW())", perm);
                }
            }

            // Sample outsource companies
            var sampleCompanies = new[]
            {
                new { Name="Addis Cleaning PLC",       ContactPerson="Abebe Kebede", Phone="+251911234567", Email="info@addiscleaning.et",   LicenseNumber="BL-1001", ContractStartDate=new DateTime(2025,1,1),  ServicesProvided="General cleaning, office cleaning, waste management" },
                new { Name="Capital Cleaning Services", ContactPerson="Tigist Haile", Phone="+251911234568", Email="contact@capital.et",       LicenseNumber="BL-1002", ContractStartDate=new DateTime(2025,3,15), ServicesProvided="Deep cleaning, window cleaning, carpet cleaning" },
                new { Name="Sparkle Cleaners",          ContactPerson="Marta Alemu",  Phone="+251911234569", Email="info@sparkle.et",          LicenseNumber="BL-1003", ContractStartDate=new DateTime(2025,5,1),  ServicesProvided="Residential cleaning, office cleaning" }
            };

            foreach (var comp in sampleCompanies)
            {
                var existing = await connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT id FROM outsource_companies WHERE company_name = @Name", new { comp.Name });
                if (existing == null)
                {
                    Console.WriteLine($"[Seeder] Creating outsource company: {comp.Name}");
                    await connection.ExecuteAsync(@"
                        INSERT INTO outsource_companies
                          (company_name,contact_person,phone,email,license_number,
                           contract_start_date,status,services_provided,created_at,updated_at)
                        VALUES (@Name,@ContactPerson,@Phone,@Email,@LicenseNumber,
                                @ContractStartDate,'Active',@ServicesProvided,NOW(),NOW())", comp);
                }
            }
        }
    }
}
