namespace Identity.Identity.Models;

using System;
using Microsoft.AspNetCore.Identity;

public class Role : IdentityRole<Guid>, IVersion
{
    public long Version { get; set; }
}