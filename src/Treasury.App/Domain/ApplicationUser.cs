using Microsoft.AspNetCore.Identity;

namespace Treasury.App.Domain;

public class ApplicationUser : IdentityUser
{
    public Guid HouseholdId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}