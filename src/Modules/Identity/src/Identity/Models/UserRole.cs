namespace Identity.Identity.Models;

using System;
using Microsoft.AspNetCore.Identity;

public class UserRole : IdentityUserRole<Guid>, IVersion
{
    public long Version { get; set; }
}