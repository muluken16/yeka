using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(string connectionString)
        {
            using var db = new MySqlConnection(connectionString);
            await db.OpenAsync();

            // ── HR: employees ────────────────────────────────────────────────
            await db.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS employees (
    id                         INT AUTO_INCREMENT PRIMARY KEY,
    employee_code              VARCHAR(50)  NOT NULL UNIQUE,
    first_name                 VARCHAR(100) NOT NULL,
    last_name                  VARCHAR(100) NOT NULL,
    middle_name                VARCHAR(100) DEFAULT '',
    gender                     VARCHAR(20)  DEFAULT '',
    date_of_birth              DATE         NULL,
    nationality                VARCHAR(100) DEFAULT 'Ethiopian',
    religion                   VARCHAR(100) DEFAULT '',
    marital_status             VARCHAR(50)  DEFAULT '',
    phone_number               VARCHAR(30)  DEFAULT '',
    email_address              VARCHAR(255) DEFAULT '',
    region                     VARCHAR(100) DEFAULT '',
    city                       VARCHAR(100) DEFAULT '',
    subcity                    VARCHAR(100) DEFAULT '',
    address                    TEXT         DEFAULT '',
    emergency_contact          VARCHAR(255) DEFAULT '',
    emergency_contact_phone    VARCHAR(30)  DEFAULT '',
    emergency_contact_relation VARCHAR(100) DEFAULT '',
    department                 VARCHAR(100) NOT NULL DEFAULT '',
    position                   VARCHAR(150) NOT NULL DEFAULT '',
    role                       VARCHAR(100) DEFAULT '',
    job_grade                  VARCHAR(50)  DEFAULT '',
    employment_type            VARCHAR(50)  DEFAULT 'Full-Time',
    hire_date                  DATE         NOT NULL,
    contract_end_date          DATE         NULL,
    employment_status          VARCHAR(30)  DEFAULT 'Active',
    highest_education          VARCHAR(100) DEFAULT '',
    field_of_study             VARCHAR(150) DEFAULT '',
    institution                VARCHAR(255) DEFAULT '',
    graduation_year            INT          NULL,
    skills                     TEXT         DEFAULT '',
    salary                     DECIMAL(12,2) DEFAULT 0.00,
    bank_account               VARCHAR(100) DEFAULT '',
    bank_name                  VARCHAR(100) DEFAULT '',
    tax_id                     VARCHAR(50)  DEFAULT '',
    pension_id                 VARCHAR(50)  DEFAULT '',
    national_id                VARCHAR(100) DEFAULT '',
    blood_type                 VARCHAR(10)  DEFAULT '',
    disability_status          VARCHAR(100) DEFAULT 'None',
    work_location              VARCHAR(150) DEFAULT '',
    supervisor_id              INT          NULL,
    second_emergency_contact   VARCHAR(255) DEFAULT '',
    second_emergency_phone     VARCHAR(30)  DEFAULT '',
    second_emergency_relation  VARCHAR(100) DEFAULT '',
    photo_url                  VARCHAR(500) DEFAULT '',
    notes                      TEXT         DEFAULT '',
    created_at                 DATETIME     DEFAULT NOW(),
    updated_at                 DATETIME     DEFAULT NOW() ON UPDATE NOW()
)");

            // ── Add new employee columns to existing databases ───────────────
            var newEmpCols = new[]
            {
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS bank_name VARCHAR(100) DEFAULT ''",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS national_id VARCHAR(100) DEFAULT ''",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS blood_type VARCHAR(10) DEFAULT ''",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS disability_status VARCHAR(100) DEFAULT 'None'",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS work_location VARCHAR(150) DEFAULT ''",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS supervisor_id INT NULL",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS second_emergency_contact VARCHAR(255) DEFAULT ''",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS second_emergency_phone VARCHAR(30) DEFAULT ''",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS second_emergency_relation VARCHAR(100) DEFAULT ''",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS photo_url VARCHAR(500) DEFAULT ''",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS notes TEXT DEFAULT ''",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS education_certificate_url VARCHAR(500) DEFAULT ''",
                // Link employees ↔ users — one employee = one user account
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS user_id INT NULL",
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS user_id_idx INT NULL", // placeholder in case ADD INDEX fails
            };
            foreach (var col in newEmpCols.Take(newEmpCols.Length - 1)) // skip placeholder
            {
                try { await db.ExecuteAsync(col); } catch { /* column already exists */ }
            }
            // Add unique index on user_id (ignore if already exists)
            try { await db.ExecuteAsync("ALTER TABLE employees ADD UNIQUE INDEX IF NOT EXISTS uq_emp_user (user_id)"); }
            catch { /* index already exists */ }

            // ── HR: employee_leaves ──────────────────────────────────────────
            await db.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS employee_leaves (
    id               INT AUTO_INCREMENT PRIMARY KEY,
    employee_id      INT          NOT NULL,
    leave_type       VARCHAR(50)  NOT NULL DEFAULT 'Annual',
    start_date       DATE         NOT NULL,
    end_date         DATE         NOT NULL,
    number_of_days   INT          NOT NULL DEFAULT 1,
    reason           TEXT         DEFAULT '',
    approval_status  VARCHAR(30)  DEFAULT 'Pending',
    approved_by      INT          NULL,
    approved_at      DATETIME     NULL,
    created_at       DATETIME     DEFAULT NOW(),
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE
)");

            // Add approved_by / approved_at if the table already existed without them.
            // Uses DATABASE() so it works regardless of connection-string format.
            var hasApprovedBy = await db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME   = 'employee_leaves'
                    AND COLUMN_NAME  = 'approved_by'");
            if (hasApprovedBy == 0)
            {
                try { await db.ExecuteAsync("ALTER TABLE employee_leaves ADD COLUMN approved_by INT NULL"); }
                catch { /* ignore */ }
            }

            var hasApprovedAt = await db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME   = 'employee_leaves'
                    AND COLUMN_NAME  = 'approved_at'");
            if (hasApprovedAt == 0)
            {
                try { await db.ExecuteAsync("ALTER TABLE employee_leaves ADD COLUMN approved_at DATETIME NULL"); }
                catch { /* ignore */ }
            }

            // ── HR: employee_attendance ──────────────────────────────────────
            await db.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS employee_attendance (
    id                INT AUTO_INCREMENT PRIMARY KEY,
    employee_id       INT          NOT NULL,
    date              DATE         NOT NULL,
    check_in_time     TIME         DEFAULT '00:00:00',
    check_out_time    TIME         DEFAULT '00:00:00',
    working_hours     DECIMAL(5,2) DEFAULT 0.00,
    overtime_hours    DECIMAL(5,2) DEFAULT 0.00,
    attendance_status VARCHAR(30)  DEFAULT 'Present',
    notes             TEXT         DEFAULT '',
    created_at        DATETIME     DEFAULT NOW(),
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE
)");

            // ── HR: employee_payroll ─────────────────────────────────────────
            await db.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS employee_payroll (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    employee_id   INT           NOT NULL,
    month         VARCHAR(20)   NOT NULL,
    year          INT           NOT NULL,
    basic_salary  DECIMAL(12,2) DEFAULT 0.00,
    allowances    DECIMAL(12,2) DEFAULT 0.00,
    overtime_pay  DECIMAL(12,2) DEFAULT 0.00,
    bonuses       DECIMAL(12,2) DEFAULT 0.00,
    deductions    DECIMAL(12,2) DEFAULT 0.00,
    tax           DECIMAL(12,2) DEFAULT 0.00,
    net_salary    DECIMAL(12,2) DEFAULT 0.00,
    status        VARCHAR(30)   DEFAULT 'Pending',
    created_at    DATETIME      DEFAULT NOW(),
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
    UNIQUE KEY uq_emp_period (employee_id, month, year)
)");

            // ── HR: employee_documents ──────────────────────────────────────
            await db.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS employee_documents (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    employee_id   INT          NOT NULL,
    doc_label     VARCHAR(200) NOT NULL DEFAULT 'Document',
    doc_type      VARCHAR(100) NOT NULL DEFAULT 'Education',
    file_name     VARCHAR(500) NOT NULL DEFAULT '',
    file_url      VARCHAR(500) NOT NULL DEFAULT '',
    file_size_kb  INT          DEFAULT 0,
    uploaded_at   DATETIME     DEFAULT NOW(),
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE
)");

            // ── HR: employee_performance_reviews ────────────────────────────
            await db.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS employee_performance_reviews (
    id                       INT AUTO_INCREMENT PRIMARY KEY,
    employee_id              INT           NOT NULL,
    review_period            VARCHAR(100)  NOT NULL,
    kpi_score                DECIMAL(5,2)  DEFAULT 0.00,
    final_rating             INT           DEFAULT 3,
    goals_achieved           DECIMAL(5,2)  DEFAULT 0.00,
    promotion_recommendation VARCHAR(10)   DEFAULT 'No',
    manager_comments         TEXT          DEFAULT '',
    reviewed_by              INT           NULL,
    created_at               DATETIME      DEFAULT NOW(),
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE
)");

            // ── Add reset_token columns to users if missing ──────────────────
            try
            {
                await db.ExecuteAsync(@"
ALTER TABLE users
    ADD COLUMN IF NOT EXISTS reset_token   VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS reset_expires DATETIME     NULL");
            }
            catch { /* already exists */ }

            // ── Transport: transport_requests ────────────────────────────────
            await db.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS transport_requests (
    id                           INT AUTO_INCREMENT PRIMARY KEY,
    request_number               VARCHAR(50)   NOT NULL UNIQUE,
    -- Requester (Mahberat user)
    mahberat_user_id             INT           NOT NULL,
    mahberat_user_name           VARCHAR(255)  DEFAULT '',
    -- Trip
    pickup_location              VARCHAR(500)  NOT NULL DEFAULT '',
    destination                  VARCHAR(500)  NOT NULL DEFAULT '',
    passenger_item_details       TEXT          DEFAULT '',
    requested_date               DATE          NULL,
    requested_time               VARCHAR(20)   DEFAULT '',
    special_instructions         TEXT          DEFAULT '',
    -- Dispatcher
    dispatcher_id                INT           NULL,
    dispatcher_name              VARCHAR(255)  DEFAULT '',
    dispatcher_notes             TEXT          DEFAULT '',
    dispatcher_action_at         DATETIME      NULL,
    -- Driver / Vehicle
    driver_id                    INT           NULL,
    driver_name                  VARCHAR(255)  DEFAULT '',
    driver_user_id               INT           NULL,
    vehicle_id                   INT           NULL,
    vehicle_plate                VARCHAR(50)   DEFAULT '',
    driver_notes                 TEXT          DEFAULT '',
    driver_action_at             DATETIME      NULL,
    -- Pickup confirmation
    pickup_confirmed_at          DATETIME      NULL,
    pickup_notes                 TEXT          DEFAULT '',
    mahberat_pickup_approved_at  DATETIME      NULL,
    mahberat_pickup_notes        TEXT          DEFAULT '',
    -- Receipt (driver upload)
    receipt_photo_url            TEXT          DEFAULT '',
    digital_receipt_url          TEXT          DEFAULT '',
    receipt_notes                TEXT          DEFAULT '',
    receipt_submitted_at         DATETIME      NULL,
    actual_kilogram              DECIMAL(10,2) NULL,
    -- Mahberat level-1 verification
    mahberat_verified_at         DATETIME      NULL,
    mahberat_verification_notes  TEXT          DEFAULT '',
    -- Staff level-2 approval
    staff_id                     INT           NULL,
    staff_name                   VARCHAR(255)  DEFAULT '',
    transport_cost               DECIMAL(12,2) NULL,
    staff_notes                  TEXT          DEFAULT '',
    staff_action_at              DATETIME      NULL,
    -- Payment
    finance_staff_id             INT           NULL,
    finance_staff_name           VARCHAR(255)  DEFAULT '',
    transaction_number           VARCHAR(100)  DEFAULT '',
    payment_proof_url            TEXT          DEFAULT '',
    paid_at                      DATETIME      NULL,
    payment_notes                TEXT          DEFAULT '',
    -- Approval form levels (for the printable form)
    level_1_mahberat_status      VARCHAR(20)   DEFAULT 'Pending',
    level_1_date                 DATETIME      NULL,
    level_2_manager_status       VARCHAR(20)   DEFAULT 'Pending',
    level_2_date                 DATETIME      NULL,
    -- Status
    status                       VARCHAR(50)   NOT NULL DEFAULT 'PendingDispatcher',
    created_at                   DATETIME      DEFAULT NOW(),
    updated_at                   DATETIME      DEFAULT NOW() ON UPDATE NOW()
)");

            // ── Transport: transport_request_logs ────────────────────────────
            await db.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS transport_request_logs (
    id                    INT AUTO_INCREMENT PRIMARY KEY,
    transport_request_id  INT          NOT NULL,
    from_status           VARCHAR(50)  DEFAULT '',
    to_status             VARCHAR(50)  NOT NULL DEFAULT '',
    actor_user_id         INT          NOT NULL DEFAULT 0,
    actor_name            VARCHAR(255) DEFAULT '',
    actor_role            VARCHAR(100) DEFAULT '',
    notes                 TEXT         DEFAULT '',
    created_at            DATETIME     DEFAULT NOW(),
    FOREIGN KEY (transport_request_id) REFERENCES transport_requests(id) ON DELETE CASCADE
)");

            // ── Add phone column to users if missing (older DBs) ────────────
            try { await db.ExecuteAsync("ALTER TABLE users ADD COLUMN IF NOT EXISTS phone VARCHAR(50) DEFAULT ''"); }
            catch { /* ignore */ }

            // ── Add driver_user_id to transport_requests if missing ──────────
            // This links the assigned driver back to the app's users table so
            // the mobile API can filter requests per-driver correctly.
            try
            {
                await db.ExecuteAsync(@"
                    ALTER TABLE transport_requests
                    ADD COLUMN IF NOT EXISTS driver_user_id INT NULL DEFAULT NULL");
            }
            catch { /* ignore if column already exists */ }

            // Backfill driver_user_id for any existing assigned requests
            try
            {
                await db.ExecuteAsync(@"
                    UPDATE transport_requests tr
                    INNER JOIN users u ON u.name = tr.driver_name
                                      AND u.role = 'driver'
                                      AND u.is_active = TRUE
                    SET tr.driver_user_id = u.id
                    WHERE tr.driver_name IS NOT NULL
                      AND tr.driver_name != ''
                      AND tr.driver_user_id IS NULL");
            }
            catch { /* ignore if transport_requests doesn't exist yet */ }

            // ── Seed default accounts for every role ─────────────────────────
            // Each account is seeded with INSERT IGNORE so it only runs once.
            // After creating the user we also ensure a linked employee record exists.

            var defaultAccounts = new[]
            {
                // ( name,                email,                        password,       role )
                ( "Super Admin",          "superadmin@yeka.et",         "admin123",     "superadmin"       ),
                ( "HR Admin",             "hr@yeka.et",                 "hr123",        "hr"               ),
                ( "Operations Manager",   "manager@yeka.et",            "manager123",   "manager"          ),
                ( "Staff Member",         "staff@yeka.et",              "staff123",     "staff"            ),
                ( "Driver Vehicle 01",    "driver1@yeka.et",            "driver123",    "driver"           ),
                ( "Dispatch Officer",     "dispatch@yeka.et",           "dispatch123",  "dispatchofficer"  ),
                ( "Wereda Mahberat",      "wereda@yeka.et",             "Wereda@123",   "wereda_mahberat"  ),
                ( "Outsource Demo",       "outsource@yeka.et",          "outsource123", "outsource"        ),
                ( "Private Co. Demo",     "private@yeka.et",            "private123",   "PrivateCompanyRep"),
            };

            foreach (var (name, email, password, role) in defaultAccounts)
            {
                // 1. Insert user if not present
                await db.ExecuteAsync(@"
INSERT IGNORE INTO users (name, email, password, role, phone, is_active, created_at, updated_at)
VALUES (@n, @e, @pw, @r, @ph, 1, NOW(), NOW())",
                    new { n = name, e = email, pw = password, r = role,
                          ph = role switch {
                              "superadmin"      => "0911000001",
                              "hr"              => "0911000002",
                              "manager"         => "0911000003",
                              "staff"           => "0911000004",
                              "driver"          => "0911000005",
                              "dispatchofficer" => "0911000006",
                              "wereda_mahberat" => "0911000007",
                              _                 => "0911000000"
                          }
                    });

                // 2. Fetch the user id (existing or just-created)
                var uid = await db.ExecuteScalarAsync<int>(
                    "SELECT id FROM users WHERE email=@e LIMIT 1", new { e = email });

                if (uid <= 0) continue;

                // 3. Check if an employee is already linked to this user
                var linkedEmp = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM employees WHERE user_id=@uid", new { uid });

                if (linkedEmp > 0) continue; // already has a profile — skip

                // 4. Build employee profile seed data per role
                var (dept, pos, grade, empType) = role switch
                {
                    "superadmin"      => ("Administration",    "System Administrator",   "Grade A", "Full-Time"),
                    "hr"              => ("Human Resources",   "HR Officer",             "Grade B", "Full-Time"),
                    "manager"         => ("Operations",        "Operations Manager",     "Grade A", "Full-Time"),
                    "staff"           => ("Cleaning Services", "Cleaning Staff",         "Grade C", "Full-Time"),
                    "driver"          => ("Logistics",         "Driver",                 "Grade C", "Full-Time"),
                    "dispatchofficer" => ("Logistics",         "Dispatch Officer",       "Grade B", "Full-Time"),
                    "wereda_mahberat" => ("Community Affairs", "Wereda Mahberat Officer","Grade B", "Full-Time"),
                    _                 => ("General",           "Staff",                  "Grade C", "Full-Time"),
                };

                var (firstName, lastName) = name.Contains(' ')
                    ? (name[..name.IndexOf(' ')], name[(name.IndexOf(' ') + 1)..])
                    : (name, "");

                var empCode = $"EMP-{role.ToUpper()[..Math.Min(3, role.Length)]}-{uid:D3}";

                var salary = role switch
                {
                    "superadmin"      => 25000m,
                    "hr"              => 18000m,
                    "manager"         => 22000m,
                    "staff"           => 8000m,
                    "driver"          => 9000m,
                    "dispatchofficer" => 11000m,
                    "wereda_mahberat" => 12000m,
                    _                 => 7000m,
                };

                // 5. Insert employee record linked to this user
                await db.ExecuteAsync(@"
INSERT INTO employees
    (employee_code, first_name, last_name, gender, date_of_birth,
     nationality, marital_status, phone_number, email_address,
     department, position, job_grade, employment_type,
     hire_date, employment_status, work_location,
     highest_education, field_of_study, institution, graduation_year,
     salary, bank_name, bank_account,
     tax_id, pension_id, national_id, blood_type,
     disability_status, skills, notes, user_id, created_at, updated_at)
VALUES
    (@ec, @fn, @ln, 'Male', '1990-01-01',
     'Ethiopian', 'Single', @ph, @em,
     @dept, @pos, @grade, @empType,
     '2023-01-01', 'Active', 'Head Office',
     'Bachelor''s Degree', 'Management', 'Addis Ababa University', 2015,
     @sal, 'Commercial Bank of Ethiopia', CONCAT('1000', @uid),
     CONCAT('TIN', @uid, '00'), CONCAT('PEN', @uid, '00'),
     CONCAT('ID', @uid, '00'), 'O+',
     'None', @skills, @notes, @uid, NOW(), NOW())",
                    new
                    {
                        ec    = empCode,
                        fn    = firstName,
                        ln    = lastName,
                        ph    = role switch {
                            "superadmin"      => "0911000001",
                            "hr"              => "0911000002",
                            "manager"         => "0911000003",
                            "staff"           => "0911000004",
                            "driver"          => "0911000005",
                            "dispatchofficer" => "0911000006",
                            "wereda_mahberat" => "0911000007",
                            _                 => "0911000000"
                        },
                        em    = email,
                        dept,
                        pos,
                        grade,
                        empType,
                        sal   = salary,
                        uid,
                        skills = role switch
                        {
                            "superadmin"      => "System Administration, IT Management, Leadership",
                            "hr"              => "Recruitment, Payroll, Labor Law, Performance Management",
                            "manager"         => "Operations Management, Team Leadership, Scheduling",
                            "staff"           => "Cleaning, Sanitation, Equipment Operation",
                            "driver"          => "Driving, Vehicle Maintenance, Route Planning",
                            "dispatchofficer" => "Dispatching, Communication, Logistics Coordination",
                            "wereda_mahberat" => "Community Coordination, Administration, Reporting",
                            _                 => "General"
                        },
                        notes = $"Default seed account for role: {role}."
                    });
            }
        }
    }
}
