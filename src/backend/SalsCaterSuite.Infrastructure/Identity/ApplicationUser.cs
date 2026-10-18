using Microsoft.AspNetCore.Identity;

namespace SalsCaterSuite.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string? Firstname { get; set; }
    public string? Lastname { get; set; }
    public string? Middle { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DateCreated { get; set; } = DateTime.UtcNow;

    public string FullName => string.IsNullOrWhiteSpace(Middle)
        ? $"{Lastname}, {Firstname}"
        : $"{Lastname}, {Firstname} {Middle}.";
}

public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }

    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}

