using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NRG.Api.Data;
using NRG.Api.Security;
using TUnit.AspNetCore;

namespace NRG.Api.Tests.AuthEndpoints;

public abstract class AuthApiFactory : TestWebApplicationFactory<Program>
{
    public static readonly string RootPath = Path.Combine(Directory.GetCurrentDirectory(), "AuthEndpoints");
    public virtual string ContentRootPath => RootPath;
    public Func<NotesDbContext> DbFactory { get; private set; } = () => throw new InvalidOperationException("DbFactory is not initialized.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile(Path.Combine(ContentRootPath, "appsettings.json"), optional: false);
        });

        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var testUser = new User()
        {
            UserId = "test-user",
            Username = "test",
            PasswordHash = TokenService.HashPassword("test"),
            Role = "Admin"
        };

        DbFactory = () => new NotesDbContext(new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options);
        using var db = DbFactory();
        db.Database.EnsureCreated();
        db.Users.Add(testUser);
        db.SaveChanges();

        builder.UseEnvironment("Development");
        builder.ConfigureServices((context, services) =>
        {
            var jwt = context.Configuration
                .GetSection(JwtOptions.SectionName)
                .Get<JwtOptions>()
                ?? throw new InvalidOperationException("JWT configuration is required.");

            services
                .AddSingleton(jwt)
                .AddScoped<TokenService>()
                .AddDbContext<NotesDbContext>(options => options.UseSqlite(connection));
        });
    }
}
