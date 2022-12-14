using System.Collections.Generic;
using System.Threading.Tasks;
using NodaTime;
using Raven.Client.Documents;
using Tweed.Data.Entities;
using Xunit;

namespace Tweed.Data.Test;

[Collection("RavenDb Collection")]
public class AppUserQueriesTest
{
    private static readonly ZonedDateTime FixedZonedDateTime =
        new(new LocalDateTime(2022, 11, 18, 15, 20), DateTimeZone.Utc, new Offset());

    private readonly IDocumentStore _store;

    public AppUserQueriesTest(RavenTestDbFixture ravenDb)
    {
        _store = ravenDb.CreateDocumentStore();
    }

    [Fact]
    public async Task AddFollower_ShouldAddFollower()
    {
        using var session = _store.OpenAsyncSession();
        AppUser user = new()
        {
            Id = "userId"
        };
        await session.StoreAsync(user);
        await session.SaveChangesAsync();
        AppUserQueries queries = new(session);

        await queries.AddFollower("leaderId", "userId", FixedZonedDateTime);

        Assert.Equal("leaderId", user.Follows[0].LeaderId);
    }

    [Fact]
    public async Task AddFollower_ShouldNotAddFollower_WhenAlreadyFollowed()
    {
        using var session = _store.OpenAsyncSession();
        AppUser user = new()
        {
            Id = "userId",
            Follows = new List<Follows>
            {
                new()
                {
                    LeaderId = "leaderId"
                }
            }
        };
        await session.StoreAsync(user);
        await session.SaveChangesAsync();
        AppUserQueries queries = new(session);

        await queries.AddFollower("leaderId", "userId", FixedZonedDateTime);

        Assert.Single(user.Follows);
    }

    [Fact]
    public async Task RemoveFollower_ShouldRemoveFollower()
    {
        using var session = _store.OpenAsyncSession();
        AppUser user = new()
        {
            Id = "userId",
            Follows = new List<Follows>
            {
                new()
                {
                    LeaderId = "leaderId"
                }
            }
        };
        await session.StoreAsync(user);

        AppUserQueries queries = new(session);
        await queries.RemoveFollower("leaderId", "userId");

        var userAfterQuery = await session.LoadAsync<AppUser>("userId");
        Assert.DoesNotContain(userAfterQuery.Follows, u => u.LeaderId == "leaderId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    public async Task GetFollowerCount_ShouldReturnFollowerCount(int givenFollowerCount)
    {
        using var session = _store.OpenAsyncSession();
        session.Advanced.WaitForIndexesAfterSaveChanges();

        AppUser leader = new()
        {
            Id = "leaderId"
        };
        await session.StoreAsync(leader);
        for (var i = 0; i < givenFollowerCount; i++)
        {
            AppUser follower = new()
            {
                Id = $"follower/${i}",
                Follows = new List<Follows>
                {
                    new()
                    {
                        LeaderId = "leaderId"
                    }
                }
            };
            await session.StoreAsync(follower);
        }

        await session.SaveChangesAsync();
        AppUserQueries queries = new(session);

        var followerCount = await queries.GetFollowerCount("leaderId");

        Assert.Equal(givenFollowerCount, followerCount);
    }

    [Fact]
    public async Task Search_ShouldReturnEmptyList_WhenNoResults()
    {
        using var session = _store.OpenAsyncSession();
        await session.StoreAsync(new AppUser
        {
            UserName = "UserName"
        });
        await session.SaveChangesAsync();
        AppUserQueries queries = new(session);

        var results = await queries.Search("noresults");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_ShouldFindMatchingAppUser()
    {
        using var session = _store.OpenAsyncSession();
        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.StoreAsync(new AppUser
        {
            UserName = "UserName"
        });
        await session.SaveChangesAsync();
        AppUserQueries queries = new(session);

        var results = await queries.Search("UserName");

        Assert.Contains(results, u => u.UserName == "UserName");
    }

    [Fact]
    public async Task Search_ShouldFindMatchingAppUser_WhenUserNamePrefixGiven()
    {
        using var session = _store.OpenAsyncSession();
        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.StoreAsync(new AppUser
        {
            UserName = "UserName"
        });
        await session.SaveChangesAsync();
        AppUserQueries queries = new(session);

        var results = await queries.Search("Use");

        Assert.Contains(results, u => u.UserName == "UserName");
    }

    [Fact]
    public async Task Search_ShouldReturn20Users()
    {
        using var session = _store.OpenAsyncSession();
        session.Advanced.WaitForIndexesAfterSaveChanges();
        for (var i = 0; i < 21; i++)
        {
            await session.StoreAsync(new AppUser
            {
                UserName = $"User-{i}"
            });
            await session.SaveChangesAsync();
        }

        AppUserQueries queries = new(session);

        var results = await queries.Search("User");

        Assert.Equal(20, results.Count);
    }

    [Fact]
    public async Task AddLike_ShouldIncreaseLikes()
    {
        using var session = _store.OpenAsyncSession();
        var appUser = new AppUser
        {
            Id = "currentUser"
        };
        await session.StoreAsync(appUser);
        var tweed = new Entities.Tweed
        {
            Id = "tweedId"
        };
        await session.StoreAsync(tweed);
        await session.SaveChangesAsync();
        var queries = new AppUserQueries(session);

        await queries.AddLike("tweedId", "currentUser", FixedZonedDateTime);

        Assert.Single(appUser.Likes);
    }

    [Fact]
    public async Task AddLike_ShouldIncreaseLikesCounter()
    {
        using var session = _store.OpenAsyncSession();
        var appUser = new AppUser
        {
            Id = "currentUser"
        };
        await session.StoreAsync(appUser);
        var tweed = new Entities.Tweed
        {
            Id = "tweedId"
        };
        await session.StoreAsync(tweed);
        await session.SaveChangesAsync();
        var queries = new AppUserQueries(session);

        await queries.AddLike("tweedId", "currentUser", FixedZonedDateTime);
        await session.SaveChangesAsync();

        var likesCounter = await session.CountersFor(tweed.Id).GetAsync("Likes");
        Assert.Equal(1, likesCounter);
    }

    [Fact]
    public async Task AddLike_ShouldNotIncreaseLikes_WhenUserHasAlreadyLiked()
    {
        using var session = _store.OpenAsyncSession();
        var appUser = new AppUser
        {
            Id = "currentUser",
            Likes = new List<TweedLike>
            {
                new()
                {
                    TweedId = "tweedId"
                }
            }
        };
        await session.StoreAsync(appUser);
        await session.SaveChangesAsync();
        var queries = new AppUserQueries(session);

        await queries.AddLike("tweedId", "currentUser", FixedZonedDateTime);

        Assert.Single(appUser.Likes);
    }

    [Fact]
    public async Task RemoveLike_ShouldDecreaseLikes()
    {
        using var session = _store.OpenAsyncSession();
        var appUser = new AppUser
        {
            Id = "currentUser",
            Likes = new List<TweedLike>
            {
                new()
                {
                    TweedId = "tweedId"
                }
            }
        };
        await session.StoreAsync(appUser);
        var tweed = new Entities.Tweed
        {
            Id = "tweedId"
        };
        await session.StoreAsync(tweed);
        await session.SaveChangesAsync();
        var queries = new AppUserQueries(session);

        await queries.RemoveLike("tweedId", "currentUser");

        Assert.Empty(appUser.Likes);
    }

    [Fact]
    public async Task RemoveLike_ShouldDecreaseLikesCounter()
    {
        using var session = _store.OpenAsyncSession();
        var appUser = new AppUser
        {
            Id = "userId"
        };
        await session.StoreAsync(appUser);
        var tweed = new Entities.Tweed
        {
            Id = "tweedId"
        };
        await session.StoreAsync(tweed);
        await session.SaveChangesAsync();
        session.CountersFor(tweed.Id).Increment("Likes");
        await session.SaveChangesAsync();
        var queries = new AppUserQueries(session);

        await queries.RemoveLike(tweed.Id, "userId");
        await session.SaveChangesAsync();

        var likesCounter = await session.CountersFor(tweed.Id).GetAsync("Likes");
        Assert.Equal(0, likesCounter);
    }

    [Fact]
    public async Task RemoveLike_ShouldNotDecreaseLikes_WhenUserAlreadyDoesntLike()
    {
        using var session = _store.OpenAsyncSession();
        var appUser = new AppUser
        {
            Id = "userId"
        };
        await session.StoreAsync(appUser);
        var tweed = new Entities.Tweed
        {
            Id = "tweedId"
        };
        await session.StoreAsync(tweed);
        await session.SaveChangesAsync();
        var queries = new AppUserQueries(session);

        await queries.RemoveLike("tweedId", "userId");

        Assert.Empty(appUser.Likes);
    }
}
