namespace CleaningManagmentSystem.Models
{
    /// <summary>
    /// Single source of truth for role string normalization.
    /// All controllers and pages must use this instead of local copies.
    /// </summary>
    public static class RoleHelper
    {
        /// <summary>
        /// Maps any raw DB role string to the canonical casing used across
        /// the web dashboard and mobile app.
        /// </summary>
        public static string NormalizeRole(string? raw) =>
            (raw ?? "").Trim().ToLower() switch
            {
                "driver"            => "driver",
                "outsource"         => "outsource",
                "privatecompanyrep" => "PrivateCompanyRep",
                "private_company_rep"  => "PrivateCompanyRep",
                "private company rep"  => "PrivateCompanyRep",
                "manager"           => "manager",
                "superadmin"        => "superadmin",
                "super admin"       => "superadmin",
                "super_admin"       => "superadmin",
                "staff"             => "staff",
                "wereda_mahberat"   => "WeredaMahberat",
                "weredamahberat"    => "WeredaMahberat",
                "wereda mahberat"   => "WeredaMahberat",
                "dispatchofficer"   => "DispatchOfficer",
                "dispatch_officer"  => "DispatchOfficer",
                "dispatch officer"  => "DispatchOfficer",
                "hr"                => "hr",
                _                   => (raw ?? "").Trim()
            };

        /// <summary>
        /// Maps a role string to its Razor Pages dashboard path.
        /// </summary>
        public static string DashboardPath(string? role) =>
            role?.ToLower() switch
            {
                "superadmin"        => "/Dashboard/SuperAdmin",
                "manager"           => "/Dashboard/Manager",
                "staff"             => "/Dashboard/Staff/Index",
                "cleaner"           => "/Dashboard/Cleaner",
                "user"              => "/Dashboard/User",
                "weredamahberat"    => "/Dashboard/WeredaMahberat",
                "wereda mahberat"   => "/Dashboard/WeredaMahberat",
                "dispatchofficer"   => "/Dashboard/DispatchOfficer/Index",
                "dispatch officer"  => "/Dashboard/DispatchOfficer/Index",
                "driver"            => "/Dashboard/Driver",
                "hr"                => "/Dashboard/HR/Index",
                _                   => "/Index"
            };
    }
}
