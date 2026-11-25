using System;
using UniHome.Infrastructure.Identity;

var hasher = new AspNetCorePasswordHasher();
var hash = hasher.HashPassword("Password123!");
Console.WriteLine("HASH:" + hash);
Console.WriteLine("VERIFY:" + hasher.VerifyPassword(hash, "Password123!"));
