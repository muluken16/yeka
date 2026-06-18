using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CleaningManagmentSystem.Pages.Dashboard.DispatchOfficer
{
    public class TransportRequestsModel : PageModel
    {
        public IActionResult OnGet()
        {
            var uid  = HttpContext.Session.GetInt32("UserId");
            var role = (HttpContext.Session.GetString("UserRole") ?? "").ToLower();

            if (uid == null)
                return RedirectToPage("/Login");

            if (role is not ("dispatchofficer" or "dispatch_officer" or "manager" or "superadmin"))
                return RedirectToPage("/Login");

            return Page();
        }
    }
}
