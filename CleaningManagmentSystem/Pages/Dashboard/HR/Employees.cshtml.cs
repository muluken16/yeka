using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.HR
{
    // ── Employee document record ─────────────────────────────────────────────
    public class EmployeeDocument
    {
        public int      Id          { get; set; }
        public int      EmployeeId  { get; set; }
        public string   DocLabel    { get; set; } = "";
        public string   DocType     { get; set; } = "Education";
        public string   FileName    { get; set; } = "";
        public string   FileUrl     { get; set; } = "";
        public int      FileSizeKb  { get; set; }
        public DateTime UploadedAt  { get; set; }

        // Helper for icon based on extension
        public string Icon => System.IO.Path.GetExtension(FileName).ToLower() switch {
            ".pdf"             => "bi-file-earmark-pdf text-danger",
            ".doc" or ".docx"  => "bi-file-earmark-word text-primary",
            ".jpg" or ".jpeg"
                or ".png"      => "bi-file-earmark-image text-success",
            _                  => "bi-file-earmark text-secondary"
        };
    }
    // ── Shared DTO used across all HR pages ──────────────────────────────────
    public class EmployeeDto
    {
        public int      Id                      { get; set; }
        public string   employee_code           { get; set; } = "";
        public string   first_name              { get; set; } = "";
        public string   last_name               { get; set; } = "";
        public string   middle_name             { get; set; } = "";
        public string   gender                  { get; set; } = "";
        public DateTime? date_of_birth          { get; set; }
        public string   nationality             { get; set; } = "";
        public string   religion                { get; set; } = "";
        public string   marital_status          { get; set; } = "";
        public string   phone_number            { get; set; } = "";
        public string   email_address           { get; set; } = "";
        public string   region                  { get; set; } = "";
        public string   city                    { get; set; } = "";
        public string   subcity                 { get; set; } = "";
        public string   address                 { get; set; } = "";
        public string   emergency_contact       { get; set; } = "";
        public string   emergency_contact_phone { get; set; } = "";
        public string   emergency_contact_relation { get; set; } = "";
        public string   department              { get; set; } = "";
        public string   position                { get; set; } = "";
        public string   role                    { get; set; } = "";
        public string   job_grade               { get; set; } = "";
        public string   employment_type         { get; set; } = "Full-Time";
        public DateTime hire_date               { get; set; } = DateTime.Today;
        public DateTime? contract_end_date      { get; set; }
        public string   employment_status       { get; set; } = "Active";
        public string   highest_education       { get; set; } = "";
        public string   field_of_study          { get; set; } = "";
        public string   institution             { get; set; } = "";
        public int?     graduation_year         { get; set; }
        public string   skills                  { get; set; } = "";
        public decimal  salary                  { get; set; }
        public string   bank_account            { get; set; } = "";
        public string   bank_name               { get; set; } = "";
        public string   tax_id                  { get; set; } = "";
        public string   pension_id              { get; set; } = "";
        public string   national_id             { get; set; } = "";
        public string   blood_type              { get; set; } = "";
        public string   disability_status       { get; set; } = "None";
        public string   work_location           { get; set; } = "";
        public int?     supervisor_id           { get; set; }
        public string   second_emergency_contact        { get; set; } = "";
        public string   second_emergency_phone          { get; set; } = "";
        public string   second_emergency_relation       { get; set; } = "";
        public string   photo_url                    { get; set; } = "";
        public string   education_certificate_url    { get; set; } = "";
        public int?     user_id                      { get; set; }
        public string   notes                   { get; set; } = "";
        public DateTime created_at              { get; set; }
        public DateTime updated_at              { get; set; }
        // Convenience aliases used in the view
        public string EmployeeCode    => employee_code;
        public string FirstName       => first_name;
        public string LastName        => last_name;
        public string MiddleName      => middle_name;
        public string Gender          => gender;
        public DateTime? DateOfBirth  => date_of_birth;
        public string Nationality     => nationality;
        public string Religion        => religion;
        public string MaritalStatus   => marital_status;
        public string PhoneNumber     => phone_number;
        public string EmailAddress    => email_address;
        public string Region          => region;
        public string City            => city;
        public string Subcity         => subcity;
        public string Address         => address;
        public string EmergencyContact        => emergency_contact;
        public string EmergencyContactPhone   => emergency_contact_phone;
        public string EmergencyContactRelation => emergency_contact_relation;
        public string Department      => department;
        public string Position        => position;
        public string Role            => role;
        public string JobGrade        => job_grade;
        public string EmploymentType  => employment_type;
        public DateTime HireDate      => hire_date;
        public DateTime? ContractEndDate => contract_end_date;
        public string EmploymentStatus => employment_status;
        public string HighestEducation => highest_education;
        public string FieldOfStudy    => field_of_study;
        public string Institution     => institution;
        public int?   GraduationYear  => graduation_year;
        public string Skills          => skills;
        public decimal Salary         => salary;
        public string BankAccount     => bank_account;
        public string BankName        => bank_name;
        public string TaxId           => tax_id;
        public string PensionId       => pension_id;
        public string NationalId      => national_id;
        public string BloodType       => blood_type;
        public string DisabilityStatus => disability_status;
        public string WorkLocation    => work_location;
        public int?   SupervisorId    => supervisor_id;
        public string SecondEmergencyContact   => second_emergency_contact;
        public string SecondEmergencyPhone     => second_emergency_phone;
        public string SecondEmergencyRelation  => second_emergency_relation;
        public string PhotoUrl                   => photo_url;
        public string EducationCertificateUrl    => education_certificate_url;
        public string Notes           => notes;
        public DateTime CreatedAt     => created_at;
        public DateTime UpdatedAt     => updated_at;

        // Display helper
        public string FullName => $"{first_name} {last_name}".Trim();
        public string Initials => (first_name.Length > 0 ? first_name[0].ToString() : "")
                                + (last_name.Length  > 0 ? last_name[0].ToString()  : "");
    }

    public class EmployeesModel : PageModel
    {
        private readonly string _cs;

        public EmployeesModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        public List<EmployeeDto> Employees { get; set; } = new();
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";
        public string Search       { get; set; } = "";
        public string DeptFilter   { get; set; } = "";
        public string StatusFilter { get; set; } = "";

        // ── Auth guard ────────────────────────────────────────────────────────
        private bool IsAuthorized() =>
            HttpContext.Session.GetString("UserRole") is "hr" or "superadmin" or "manager";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet(string? search, string? dept, string? status)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            Search       = search ?? "";
            DeptFilter   = dept   ?? "";
            StatusFilter = status ?? "";

            SuccessMessage = TempData["Success"]?.ToString() ?? "";
            ErrorMessage   = TempData["Error"]?.ToString()   ?? "";

            LoadEmployees();
            return Page();
        }

        private void LoadEmployees()
        {
            using var db = new MySqlConnection(_cs);
            var sql = @"SELECT * FROM employees WHERE 1=1
                        AND (@s = '' OR CONCAT(first_name,' ',last_name,' ',email_address,' ',department) LIKE @like)
                        AND (@d = '' OR department = @d)
                        AND (@st = '' OR employment_status = @st)
                        ORDER BY first_name ASC";
            Employees = db.Query<EmployeeDto>(sql, new
            {
                s    = Search,
                like = $"%{Search}%",
                d    = DeptFilter,
                st   = StatusFilter
            }).ToList();
        }

        // ── ADD ───────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAddAsync(
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
                // Handle photo upload
                string photoUrl = "";
                if (PhotoFile != null && PhotoFile.Length > 0)
                    photoUrl = await SaveUploadAsync(PhotoFile, "employees");

                using var db = new MySqlConnection(_cs);

                // Generate employee code
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

                // Save education documents
                await SaveDocumentsAsync(db, empId, EduFiles, EduFileLabels, EduFileTypes);

                TempData["Success"] = $"Employee {FirstName} {LastName} registered successfully (Code: {code}).";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }
            return RedirectToPage();
        }

        // ── UPDATE ────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostUpdateAsync(
            int Id, string EmployeeCode,
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
            string? ExistingPhotoUrl, IFormFile? PhotoFile,
            List<IFormFile>? EduFiles, List<string>? EduFileLabels, List<string>? EduFileTypes)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            try
            {
                // Handle photo update
                string photoUrl = ExistingPhotoUrl ?? "";
                if (PhotoFile != null && PhotoFile.Length > 0)
                    photoUrl = await SaveUploadAsync(PhotoFile, "employees");

                using var db = new MySqlConnection(_cs);
                db.Execute(@"UPDATE employees SET
                    first_name=@Fn, last_name=@Ln, middle_name=@Mn,
                    gender=@Gen, date_of_birth=@Dob, nationality=@Nat, religion=@Rel, marital_status=@Mar,
                    phone_number=@Ph, email_address=@Em,
                    region=@Reg, city=@Cit, subcity=@Sub, address=@Adr,
                    emergency_contact=@EC, emergency_contact_phone=@ECP, emergency_contact_relation=@ECR,
                    second_emergency_contact=@EC2, second_emergency_phone=@ECP2, second_emergency_relation=@ECR2,
                    department=@Dept, position=@Pos, role=@Rol, job_grade=@Jg, employment_type=@ET,
                    hire_date=@Hd, contract_end_date=@Ced,
                    highest_education=@HE, field_of_study=@FoS, institution=@Inst,
                    graduation_year=@GY, skills=@Sk,
                    salary=@Sal, bank_account=@BA, bank_name=@BN, tax_id=@TI, pension_id=@PI,
                    national_id=@NID, blood_type=@BT, disability_status=@DS,
                    work_location=@WL, notes=@Nt,
                    photo_url=@Photo,
                    updated_at=NOW()
                    WHERE id=@Id",
                    new
                    {
                        Id,
                        Fn = FirstName, Ln = LastName, Mn = MiddleName ?? "",
                        Gen = Gender ?? "", Dob = DateOfBirth, Nat = Nationality ?? "",
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

                // Append new education documents (existing ones are kept)
                await SaveDocumentsAsync(db, Id, EduFiles, EduFileLabels, EduFileTypes);

                TempData["Success"] = $"Employee {FirstName} {LastName} updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Update error: {ex.Message}";
            }
            return RedirectToPage();
        }

        // ── DELETE DOCUMENT ───────────────────────────────────────────────────
        public IActionResult OnPostDeleteDocument(int docId, int empId)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");
            using var db = new MySqlConnection(_cs);
            // Get file path to delete from disk
            var fileUrl = db.QueryFirstOrDefault<string>(
                "SELECT file_url FROM employee_documents WHERE id=@Id", new { Id = docId });
            db.Execute("DELETE FROM employee_documents WHERE id=@Id", new { Id = docId });
            if (!string.IsNullOrEmpty(fileUrl))
            {
                var diskPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(diskPath))
                    System.IO.File.Delete(diskPath);
            }
            TempData["Success"] = "Document deleted.";
            return RedirectToPage();
        }

        // ── TOGGLE STATUS ─────────────────────────────────────────────────────
        public IActionResult OnPostToggleStatus(int id, string status)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            var newStatus = status == "Active" ? "Inactive" : "Active";
            using var db  = new MySqlConnection(_cs);
            db.Execute("UPDATE employees SET employment_status=@s, updated_at=NOW() WHERE id=@id",
                new { s = newStatus, id });

            TempData["Success"] = $"Employee status changed to {newStatus}.";
            return RedirectToPage();
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
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
