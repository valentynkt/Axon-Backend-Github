namespace Identity.Identity.Models;

using System;
using Microsoft.AspNetCore.Identity;

public class UserClaim : IdentityUserClaim<Guid>, IVersion
{
    public long Version { get; set; }
}