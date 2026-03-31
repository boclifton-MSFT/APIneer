using APIneer.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace APIneer.Api.Tests;

/// <summary>
/// Shared test fixture that boots the API with an in-memory SQLite database.
/// </summary>
public class ApiTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the production DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Use a shared in-memory SQLite connection so the schema persists
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection)
                    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        });
    }

    /// <summary>
    /// Returns a client and resets the database so each test starts with clean state.
    /// </summary>
    public new HttpClient CreateClient()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Assertions.RemoveRange(db.Assertions);
        db.RequestHistory.RemoveRange(db.RequestHistory);
        db.ApiRequests.RemoveRange(db.ApiRequests);
        db.CollectionFolders.RemoveRange(db.CollectionFolders);
        db.Collections.RemoveRange(db.Collections);
        db.EnvironmentVariables.RemoveRange(db.EnvironmentVariables);
        db.Environments.RemoveRange(db.Environments);
        db.Workspaces.RemoveRange(db.Workspaces);
        db.SaveChanges();
        return base.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Drop and recreate so the schema always matches the current model
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();
    }
}
