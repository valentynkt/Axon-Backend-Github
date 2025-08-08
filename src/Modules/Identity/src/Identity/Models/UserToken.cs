namespace Identity.Identity.Models;

using System;
using Microsoft.AspNetCore.Identity;

public class UserToken : IdentityUserToken<Guid>, IVersion
{
    public long Version { get; set; }
}