using System;
using System.Data;
using System.Threading.Tasks;
using MySqlConnector;
using Dapper;

namespace SeedEmployees
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string cs = "Server=127.0.0.1;Port=3306;Database=yeka_cleaning;User=root;Password=;";
            using var db = new MySqlConnection(cs);
            await db.OpenAsync();

            Console.WriteLine("Connecting to database and seeding 10 employees...");

            var employees = new[]
            {
                new { Name = "Abebe Bikila", Gender = "Male", Email = "abebe@yeka.et", Dept = "Cleaning Services", Pos = "Cleaning Staff", Salary = 8200m, Phone = "0911223344" },
                new { Name = "Almaz Ayana", Gender = "Female", Email = "almaz@yeka.et", Dept = "Cleaning Services", Pos = "Cleaning Staff", Salary = 8200m, Phone = "0911223345" },
                new { Name = "Kenenisa Bekele", Gender = "Male", Email = "kenenisa@yeka.et", Dept = "Operations", Pos = "Team Leader", Salary = 12000m, Phone = "0911223346" },
                new { Name = "Derartu Tulu", Gender = "Female", Email = "derartu@yeka.et", Dept = "Cleaning Services", Pos = "Cleaning Staff", Salary = 8400m, Phone = "0911223347" },
                new { Name = "Haile Gebrselassie", Gender = "Male", Email = "haile@yeka.et", Dept = "Operations", Pos = "Supervisor", Salary = 15000m, Phone = "0911223348" },
                new { Name = "Meseret Defar", Gender = "Female", Email = "meseret@yeka.et", Dept = "Cleaning Services", Pos = "Cleaning Staff", Salary = 8400m, Phone = "0911223349" },
                new { Name = "Tirunesh Dibaba", Gender = "Female", Email = "tirunesh@yeka.et", Dept = "Cleaning Services", Pos = "Cleaning Staff", Salary = 8500m, Phone = "0911223350" },
                new { Name = "Sileshi Sihine", Gender = "Male", Email = "sileshi@yeka.et", Dept = "Logistics", Pos = "Support Staff", Salary = 9000m, Phone = "0911223351" },
                new { Name = "Tariku Bekele", Gender = "Male", Email = "tariku@yeka.et", Dept = "Cleaning Services", Pos = "Cleaning Staff", Salary = 8200m, Phone = "0911223352" },
                new { Name = "Feyisa Lilesa", Gender = "Male", Email = "feyisa@yeka.et", Dept = "Logistics", Pos = "Support Staff", Salary = 9200m, Phone = "0911223353" }
            };

            int index = 1;
            foreach (var emp in employees)
            {
                var parts = emp.Name.Split(' ');
                var fn = parts[0];
                var ln = parts.Length > 1 ? parts[1] : "";
                var code = $"EMP-SEED-{index:D3}";

                // 1. Check if user already exists
                var userCount = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users WHERE email=@e", new { e = emp.Email });
                
                int uid;
                if (userCount > 0)
                {
                    uid = await db.ExecuteScalarAsync<int>("SELECT id FROM users WHERE email=@e", new { e = emp.Email });
                    Console.WriteLine($"User {emp.Email} already exists. Verifying employee profile...");
                }
                else
                {
                    // Create user account
                    await db.ExecuteAsync(@"
                        INSERT INTO users (name, email, password, role, phone, is_active, created_at, updated_at) 
                        VALUES (@n, @e, 'Yeka@1234', 'staff', @p, 1, NOW(), NOW())", 
                        new { n = emp.Name, e = emp.Email, p = emp.Phone });
                    
                    uid = await db.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID()");
                    Console.WriteLine($"Created User: {emp.Name} ({emp.Email})");
                }

                // 2. Check if employee record already exists
                var empCount = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM employees WHERE user_id=@uid", new { uid });
                if (empCount == 0)
                {
                    await db.ExecuteAsync(@"
                        INSERT INTO employees 
                        (employee_code, first_name, last_name, gender, date_of_birth, nationality, religion, marital_status,
                         phone_number, email_address, region, city, subcity, address,
                         emergency_contact, emergency_contact_phone, emergency_contact_relation,
                         department, position, employment_status, hire_date, user_id, salary, bank_name, bank_account,
                         tax_id, pension_id, national_id, blood_type, disability_status, work_location, notes, created_at, updated_at) 
                        VALUES 
                        (@code, @fn, @ln, @gender, '1992-05-15', 'Ethiopian', 'Christian', 'Single',
                         @phone, @email, 'Addis Ababa', 'Addis Ababa', 'Yeka', 'Wereda 11',
                         'Emergency Contact Name', '0911000000', 'Sibling',
                         @dept, @pos, 'Active', '2023-01-01', @uid, @salary, 'Commercial Bank of Ethiopia', CONCAT('1000', @uid),
                         CONCAT('TIN-', @uid), CONCAT('PEN-', @uid), CONCAT('NID-', @uid), 'O+', 'None', 'Head Office', 
                         'Sample employee profile seeded automatically.', NOW(), NOW())",
                        new { code, fn, ln, gender = emp.Gender, phone = emp.Phone, email = emp.Email, dept = emp.Dept, pos = emp.Pos, uid, salary = emp.Salary });

                    Console.WriteLine($"  -> Seeded Employee Profile: {code} for {emp.Name}");
                }
                else
                {
                    Console.WriteLine($"  -> Employee Profile already exists for {emp.Name}");
                }

                index++;
            }

            Console.WriteLine("Seeding completed successfully.");
        }
    }
}
