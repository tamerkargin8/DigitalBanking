using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Delegations;
using DigitalBanking.Application.DTOs.Identity;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Security;
using DigitalBanking.Infrastructure.Services;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Tests;

public class IdentityAndDelegationTests
{
    private static async Task<(TestDb Db, DigitalBanking.Infrastructure.Persistence.BankDbContext Context,
        UserIdentityService Identity, IApprovalAuthorizationService Authorization, PasswordHasher Hasher)>
        CreateIdentityFixtureAsync()
    {
        var db = await TestDb.CreateAsync();
        var context = db.NewContext();
        var audit = new AuditService(context);
        var authorization = new ApprovalAuthorizationService(context);
        var hasher = new PasswordHasher();
        var identity = new UserIdentityService(context, hasher, audit, authorization);
        return (db, context, identity, authorization, hasher);
    }

    [Fact]
    public async Task FirstOwnerSetupWorksOnceAndRegistrationCannotCreateOwner()
    {
        var f = await CreateIdentityFixtureAsync();
        await using var _ = f.Db;

        Assert.False(await f.Identity.OwnerExistsAsync());
        var owner = await f.Identity.SetupOwnerAsync(new SetupOwnerRequest
        {
            Username = "local-owner",
            Email = "owner@demo.invalid",
            FirstName = "Local",
            LastName = "Owner",
            Password = "SyntheticOwner1",
            ConfirmPassword = "SyntheticOwner1"
        });
        Assert.Equal("Owner", owner.Role);
        Assert.Equal("Active", owner.Status);
        Assert.True(await f.Identity.OwnerExistsAsync());

        await Assert.ThrowsAsync<ConflictException>(() => f.Identity.SetupOwnerAsync(new SetupOwnerRequest
        {
            Username = "second-owner",
            Email = "second@demo.invalid",
            FirstName = "Second",
            LastName = "Owner",
            Password = "SyntheticOwner1",
            ConfirmPassword = "SyntheticOwner1"
        }));

        var registered = await f.Identity.RegisterAsync(new RegisterRequest
        {
            Username = "new-member",
            Email = "new@demo.invalid",
            FirstName = "New",
            LastName = "Member",
            Password = "SyntheticMember1",
            ConfirmPassword = "SyntheticMember1"
        });
        Assert.Equal("PendingActivation", registered.Status);

        var member = await f.Context.Users.SingleAsync(x => x.Username == "new-member");
        Assert.Equal(UserRole.Customer, member.Role);
        Assert.Equal(UserStatus.PendingActivation, member.Status);
    }

    [Fact]
    public async Task ConcurrentOwnerSetupCreatesOneOwnerAndReturnsConflictForLoser()
    {
        var path = Path.Combine(Path.GetTempPath(), $"digitalbanking-owner-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=30")
            .Options;

        try
        {
            await using (var setup = new BankDbContext(options))
                await setup.Database.EnsureCreatedAsync();

            var hasher = new PasswordHasher();
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<Exception?> AttemptAsync(string username)
            {
                await gate.Task;
                await using var context = new BankDbContext(options);
                var identity = new UserIdentityService(
                    context, hasher, new AuditService(context), new ApprovalAuthorizationService(context));
                try
                {
                    await identity.SetupOwnerAsync(new SetupOwnerRequest
                    {
                        Username = username,
                        Email = username + "@demo.invalid",
                        FirstName = "Concurrent",
                        LastName = "Owner",
                        Password = "SyntheticOwner1",
                        ConfirmPassword = "SyntheticOwner1"
                    });
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }

            // Two real DbContexts and two separate SQLite connections race on an
            // empty database. The filtered unique index must decide the winner,
            // and the loser must be translated to a domain conflict (HTTP 409).
            var attempts = new[]
            {
                AttemptAsync("race-owner-a"),
                AttemptAsync("race-owner-b")
            };
            gate.SetResult();
            var results = await Task.WhenAll(attempts);

            Assert.Single(results, result => result is null);
            Assert.Single(results, result => result is ConflictException);

            await using var verify = new BankDbContext(options);
            Assert.Equal(1, await verify.Users.CountAsync(x => x.Role == UserRole.Owner));
            Assert.Equal(1, await verify.Users.CountAsync(x => x.Role == UserRole.Owner && x.Status == UserStatus.Active));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task RegistrationValidatesDuplicatesAndPasswords()
    {
        var f = await CreateIdentityFixtureAsync();
        await using var _ = f.Db;

        var request = new RegisterRequest
        {
            Username = "member-one",
            Email = "one@demo.invalid",
            FirstName = "One",
            LastName = "Member",
            Password = "SyntheticMember1",
            ConfirmPassword = "SyntheticMember1"
        };
        await f.Identity.RegisterAsync(request);

        await Assert.ThrowsAsync<ConflictException>(() => f.Identity.RegisterAsync(new RegisterRequest
        {
            Username = "member-one",
            Email = "other@demo.invalid",
            FirstName = "One",
            LastName = "Member",
            Password = "SyntheticMember1",
            ConfirmPassword = "SyntheticMember1"
        }));
        await Assert.ThrowsAsync<ConflictException>(() => f.Identity.RegisterAsync(new RegisterRequest
        {
            Username = "member-two",
            Email = "one@demo.invalid",
            FirstName = "Two",
            LastName = "Member",
            Password = "SyntheticMember1",
            ConfirmPassword = "SyntheticMember1"
        }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Identity.RegisterAsync(new RegisterRequest
        {
            Username = "member-three",
            Email = "three@demo.invalid",
            FirstName = "Three",
            LastName = "Member",
            Password = "short",
            ConfirmPassword = "short"
        }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Identity.RegisterAsync(new RegisterRequest
        {
            Username = "member-four",
            Email = "not-an-email",
            FirstName = "Four",
            LastName = "Member",
            Password = "SyntheticMember1",
            ConfirmPassword = "SyntheticMember1"
        }));
    }

    [Fact]
    public async Task PendingActivationCannotLoginButActivationAllowsLoginAndCreatesCustomer()
    {
        var f = await CreateIdentityFixtureAsync();
        await using var _ = f.Db;

        var owner = await f.Identity.SetupOwnerAsync(new SetupOwnerRequest
        {
            Username = "owner-login",
            Email = "owner-login@demo.invalid",
            FirstName = "Owner",
            LastName = "Login",
            Password = "SyntheticOwner1",
            ConfirmPassword = "SyntheticOwner1"
        });
        var ownerUser = await f.Context.Users.SingleAsync(x => x.Id == owner.Id);
        var ownerActor = new DigitalBanking.Application.Common.ActorContext(ownerUser.Id, ownerUser.Username, ownerUser.Role);

        var registration = await f.Identity.RegisterAsync(new RegisterRequest
        {
            Username = "pending-login",
            Email = "pending-login@demo.invalid",
            FirstName = "Pending",
            LastName = "Login",
            Password = "SyntheticMember1",
            ConfirmPassword = "SyntheticMember1"
        });
        var pending = await f.Context.Users.SingleAsync(x => x.Id == registration.UserId);

        var auth = new AuthService(
            f.Context,
            f.Hasher,
            new JwtTokenService(TestSupport.CreateConfiguration()),
            new AuditService(f.Context),
            new InMemoryLoginAttemptLimiter());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            auth.LoginAsync(new DigitalBanking.Application.DTOs.Auth.LoginRequest
            {
                Username = pending.Username,
                Password = "SyntheticMember1"
            }));

        await f.Identity.ActivateAsync(pending.Id, ownerActor);
        var customerProfile = await f.Context.Customers.SingleOrDefaultAsync(x => x.UserId == pending.Id);
        Assert.NotNull(customerProfile);

        var login = await auth.LoginAsync(new DigitalBanking.Application.DTOs.Auth.LoginRequest
        {
            Username = pending.Username,
            Password = "SyntheticMember1"
        });
        Assert.Equal("Customer", login.Role);
        Assert.Equal("Active", login.Status);
    }

    [Fact]
    public async Task OwnerCannotBeDisabledDemotedOrAssignedAsRole()
    {
        var f = await CreateIdentityFixtureAsync();
        await using var _ = f.Db;
        var owner = await f.Identity.SetupOwnerAsync(new SetupOwnerRequest
        {
            Username = "owner-protect",
            Email = "owner-protect@demo.invalid",
            FirstName = "Owner",
            LastName = "Protect",
            Password = "SyntheticOwner1",
            ConfirmPassword = "SyntheticOwner1"
        });
        var ownerUser = await f.Context.Users.SingleAsync(x => x.Id == owner.Id);
        var ownerActor = new DigitalBanking.Application.Common.ActorContext(ownerUser.Id, ownerUser.Username, ownerUser.Role);

        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Identity.DisableAsync(owner.Id, ownerActor));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Identity.ChangeRoleAsync(owner.Id, "Customer", ownerActor));

        var member = await f.Identity.RegisterAsync(new RegisterRequest
        {
            Username = "role-member",
            Email = "role-member@demo.invalid",
            FirstName = "Role",
            LastName = "Member",
            Password = "SyntheticMember1",
            ConfirmPassword = "SyntheticMember1"
        });
        await f.Identity.ActivateAsync(member.UserId, ownerActor);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Identity.ChangeRoleAsync(member.UserId, "Owner", ownerActor));
    }

    [Fact]
    public async Task DelegationLimitCurrencyExpiryAndRevocationAreServerSide()
    {
        var f = await CreateIdentityFixtureAsync();
        await using var _ = f.Db;
        var owner = await f.Identity.SetupOwnerAsync(new SetupOwnerRequest
        {
            Username = "owner-delegation",
            Email = "owner-delegation@demo.invalid",
            FirstName = "Owner",
            LastName = "Delegation",
            Password = "SyntheticOwner1",
            ConfirmPassword = "SyntheticOwner1"
        });
        var member = await f.Identity.RegisterAsync(new RegisterRequest
        {
            Username = "delegated-member",
            Email = "delegated@demo.invalid",
            FirstName = "Delegated",
            LastName = "Member",
            Password = "SyntheticMember1",
            ConfirmPassword = "SyntheticMember1"
        });
        var ownerActor = new DigitalBanking.Application.Common.ActorContext(owner.Id, "owner-delegation", UserRole.Owner);
        await f.Identity.ActivateAsync(member.UserId, ownerActor);
        await f.Identity.ChangeRoleAsync(member.UserId, "DelegatedApprover", ownerActor);

        var delegationService = new DelegationService(f.Context, new AuditService(f.Context), f.Authorization);
        var delegation = await delegationService.CreateAsync(new CreateDelegationRequest
        {
            UserId = member.UserId,
            Operation = "Transfer",
            Currency = "TRY",
            MaxAmount = 100_000m
        }, ownerActor);

        Assert.True((await f.Authorization.CheckAsync(member.UserId, ApprovalOperation.Transfer, "TRY", 100_000m)).Allowed);
        Assert.False((await f.Authorization.CheckAsync(member.UserId, ApprovalOperation.Transfer, "TRY", 100_000.01m)).Allowed);

        await Assert.ThrowsAsync<BusinessRuleException>(() => delegationService.CreateAsync(new CreateDelegationRequest
        {
            UserId = member.UserId,
            Operation = "Transfer",
            Currency = "USD",
            MaxAmount = 1_000m,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        }, ownerActor));
        Assert.False((await f.Authorization.CheckAsync(member.UserId, ApprovalOperation.Transfer, "USD", 1m)).Allowed);
        Assert.False((await f.Authorization.CheckAsync(member.UserId, ApprovalOperation.Deposit, "TRY", 1m)).Allowed);

        await delegationService.DeactivateAsync(delegation.Id, ownerActor);
        Assert.False((await f.Authorization.CheckAsync(member.UserId, ApprovalOperation.Transfer, "TRY", 1m)).Allowed);
    }

    [Fact]
    public async Task DelegatedApproverCannotManageDelegationsOrRoles()
    {
        var f = await CreateIdentityFixtureAsync();
        await using var _ = f.Db;
        await f.Identity.SetupOwnerAsync(new SetupOwnerRequest
        {
            Username = "owner-auth",
            Email = "owner-auth@demo.invalid",
            FirstName = "Owner",
            LastName = "Auth",
            Password = "SyntheticOwner1",
            ConfirmPassword = "SyntheticOwner1"
        });
        var delegated = await TestSupport.AddUserAsync(f.Context, "delegated-auth", "hash", UserRole.DelegatedApprover);
        var delegatedActor = new DigitalBanking.Application.Common.ActorContext(delegated.Id, delegated.Username, delegated.Role);

        var delegationService = new DelegationService(f.Context, new AuditService(f.Context), f.Authorization);
        await Assert.ThrowsAsync<ForbiddenException>(() => delegationService.CreateAsync(new CreateDelegationRequest
        {
            UserId = delegated.Id,
            Operation = "Transfer",
            Currency = "TRY",
            MaxAmount = 1_000_000m
        }, delegatedActor));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Identity.ChangeRoleAsync(delegated.Id, "Customer", delegatedActor));
    }
}
