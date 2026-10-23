using backend.Domain.Cases;
using backend.Domain.Clients;
using backend.Domain.Users;
using backend.Features.Clients.Workspace;
using NUlid;
using Xunit;

namespace Backend.Tests;

public sealed class ClientDetailQueryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaseAmountsFollowExplicitReadVisibility(bool canViewFinancials)
    {
        await using var db = TestDatabase.Create();
        var actor = User.Create("curator@example.test", "@curator", "test-hash", User.UserRole.Curator, "Curator");
        var client = Client.Create("Student", Email.From("student@example.test"),
            ContactInfo.Create("123456789"), new Financials(100, 200));
        db.AddRange(actor, client);
        db.Cases.Add(Case.Create(client.Id, actor.Id, Case.CaseType.Other, 150));
        await db.SaveChangesAsync();

        var query = new ClientDetailQueries(db, new ClientProductQueries(db), new ClientActivityQueries(db));
        var detail = await query.GetAsync(client.Id, canViewFinancials, default);

        Assert.NotNull(detail);
        Assert.Null(detail.Financials);
        Assert.Equal(canViewFinancials ? 150m : (decimal?)null, Assert.Single(detail.Cases).Amount);
        Assert.Empty(detail.Products);
        Assert.Equal(1, detail.Stats.TotalCasesCount);
        Assert.Equal(1, detail.CasesSummary.TotalCasesCount);
    }

    [Fact]
    public async Task MissingClientDoesNotProduceAnEmptyFakeWorkspace()
    {
        await using var db = TestDatabase.Create();
        var query = new ClientDetailQueries(db, new ClientProductQueries(db), new ClientActivityQueries(db));
        Assert.Null(await query.GetAsync(Ulid.NewUlid(), false, default));
    }
}
