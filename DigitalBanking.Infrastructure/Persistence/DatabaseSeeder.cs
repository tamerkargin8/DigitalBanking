using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        BankDbContext context,
        PasswordHasher passwordHasher)
    {
        await context.Database.MigrateAsync();

        await SeedUsersAsync(context, passwordHasher);
    }

    private static async Task SeedUsersAsync(
        BankDbContext context,
        PasswordHasher passwordHasher)
    {
        var adminExists = await context.Users
            .AnyAsync(x => x.Username == "admin");

        if (!adminExists)
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                PasswordHash = passwordHasher.Hash("1234"),
                Role = UserRole.Admin,
                CreatedDate = DateTime.UtcNow
            });
        }

        var standardUserExists = await context.Users
            .AnyAsync(x => x.Username == "tamer");

        if (!standardUserExists)
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = "tamer",
                PasswordHash = passwordHasher.Hash("1234"),
                Role = UserRole.User,
                CreatedDate = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }
}