using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.HR
{
    public class AddEmployeeModel : PageModel
    {
        private readonly string _cs;

        public AddEmployeeModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        private bool IsAuthorized() =>
            HttpContext.Session.GetString("UserRole") is "hr" or "superadmin" or "manager";

        public IActionResult OnGet()
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(
            string FirstName, string LastName, string? MiddleName,
            string? Gender, string? DateOfBirth, string? Nationality, string? Religion, string? MaritalStatus,
            string? PhoneNumber, string? EmailAddress,
            string? Region, string? City, string? Subcity, string? Address,
            string? EmergencyContact, string? EmergencyContactPhone, string? EmergencyContactRelation,
            string? SecondEmergencyContact, string? SecondEmergencyPhone, string? SecondEmergencyRelation,
            string Department, string Position, string? Role, string? JobGrade,
            string? EmploymentType, string HireDate, string? ContractEndDate,
            string? HighestEducation, string? FieldOfStudy, string? Institution,
            int? GraduationYear, string? Skills,
            decimal? Salary, string? BankAccount, string? BankName, string? TaxId, string? PensionId,
            string? NationalId, string? BloodType, string? DisabilityStatus,
            string? WorkLocation, string? Notes,
            IFormFile? PhotoFile,
            List<IFormFile>? EduFiles, List<string>? EduFileLabels, List<string>? EduFileTypes)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            try
            {
                string photoUrl = "";
                if (PhotoFile != null && PhotoFile.Length > 0)
                    photoUrl = await SaveUploadAsync(PhotoFile, "employees");

                using var db = new MySqlConnection(_cs);
                var count = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM employees") + 1;
                var code  = $"EMP-{count:D4}";

                var empId = db.QueryFirstOrDefault<int>(@"INSERT INTO employees
                    (employee_code, first_name, last_name, middle_name,
                     gender, date_of_birth, nationality, religion, marital_status,
                     phone_number, email_address,
                     region, city, subcity, address,
                     emergency_contact, emergency_contact_phone, emergency_contact_relation,
                     second_emergency_contact, second_emergency_phone, second_emergency_relation,
                     department, position, role, job_grade, employment_type,
                     hire_date, contract_end_date, employment_status,
                     highest_education, field_of_study, institution, graduation_year, skills,
                     salary, bank_account, bank_name, tax_id, pension_id,
                     national_id, blood_type, disability_status, work_location, notes,
                     photo_url, created_at, updated_at)
                    VALUES
                    (@Code, @Fn, @Ln, @Mn,
                     @Gen, @Dob, @Nat, @Rel, @Mar,
                     @Ph, @Em,
                     @Reg, @Cit, @Sub, @Adr,
                     @EC, @ECP, @ECR,
                     @EC2, @ECP2, @ECR2,
                     @Dept, @Pos, @Rol, @Jg, @ET,
                     @Hd, @Ced, 'Active',
                     @HE, @FoS, @Inst, @GY, @Sk,
                     @Sal, @BA, @BN, @TI, @PI,
                     @NID, @BT, @DS, @WL, @Nt,
                     @Photo, NOW(), NOW());
                    SELECT LAST_INSERT_ID();",
                    new
                    {
                        Code = code,
                        Fn = FirstName, Ln = LastName, Mn = MiddleName ?? "",
                        Gen = Gender ?? "", Dob = DateOfBirth, Nat = Nationality ?? "Ethiopian",
                        Rel = Religion ?? "", Mar = MaritalStatus ?? "",
                        Ph = PhoneNumber ?? "", Em = EmailAddress ?? "",
                        Reg = Region ?? "", Cit = City ?? "", Sub = Subcity ?? "", Adr = Address ?? "",
                        EC = EmergencyContact ?? "", ECP = EmergencyContactPhone ?? "", ECR = EmergencyContactRelation ?? "",
                        EC2 = SecondEmergencyContact ?? "", ECP2 = SecondEmergencyPhone ?? "", ECR2 = SecondEmergencyRelation ?? "",
                        Dept = Department, Pos = Position, Rol = Role ?? "", Jg = JobGrade ?? "",
                        ET = EmploymentType ?? "Full-Time",
                        Hd = HireDate, Ced = string.IsNullOrEmpty(ContractEndDate) ? (object)DBNull.Value : ContractEndDate,
                        HE = HighestEducation ?? "", FoS = FieldOfStudy ?? "", Inst = Institution ?? "",
                        GY = (object?)GraduationYear ?? DBNull.Value, Sk = Skills ?? "",
                        Sal = Salary ?? 0m, BA = BankAccount ?? "", BN = BankName ?? "", TI = TaxId ?? "", PI = PensionId ?? "",
                        NID = NationalId ?? "", BT = BloodType ?? "", DS = DisabilityStatus ?? "None",
                        WL = WorkLocation ?? "", Nt = Notes ?? "",
                        Photo = photoUrl
                    });

                await SaveDocumentsAsync(db, empId, EduFiles, EduFileLabels, EduFileTypes);

                TempData["Success"] = $"Employee {FirstName} {LastName} registered successfully (Code: {code}).";
                return RedirectToPage("/Dashboard/HR/Employees");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToPage("/Dashboard/HR/Employees");
            }
        }

        private async Task<string> SaveUploadAsync(IFormFile file, string subFolder)
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", subFolder);
            Directory.CreateDirectory(uploadsDir);
            var ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            using (var stream = System.IO.File.Create(filePath))
                await file.CopyToAsync(stream);
            return $"/uploads/{subFolder}/{fileName}";
        }

        private async Task SaveDocumentsAsync(MySqlConnection db, int empId,
            List<IFormFile>? files, List<string>? labels, object? docTypes)
        {
            if (files == null || files.Count == 0) return;
            var typeList = docTypes as List<string>;
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (f == null || f.Length == 0) continue;
                var label   = (labels != null && i < labels.Count && !string.IsNullOrWhiteSpace(labels[i]))
                              ? labels[i]
                              : Path.GetFileNameWithoutExtension(f.FileName);
                var docType = (typeList != null && i < typeList.Count && !string.IsNullOrWhiteSpace(typeList[i]))
                              ? typeList[i]
                              : "Education";
                var fileUrl = await SaveUploadAsync(f, "employee-docs");
                db.Execute(@"INSERT INTO employee_documents
                    (employee_id, doc_label, doc_type, file_name, file_url, file_size_kb, uploaded_at)
                    VALUES (@EId, @Lbl, @Typ, @FName, @FUrl, @FSz, NOW())",
                    new {
                        EId   = empId,
                        Lbl   = label,
                        Typ   = docType,
                        FName = f.FileName,
                        FUrl  = fileUrl,
                        FSz   = (int)(f.Length / 1024)
                    });
            }
        }
    }
}
