using Microsoft.AspNetCore.Identity;

namespace PlataformaCredito.Models;

public class ApplicationUser : IdentityUser
{
    public Cliente? Cliente { get; set; }
}
