using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NRG.Api.Data;
using NRG.Api.Security;
using System.Text;

namespace NRG.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var jwt = builder.Configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required.");
        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
        }

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        var connectionString = builder.Configuration.GetConnectionString("Notes") ?? "Data Source=notes.db";
        if (connectionString.EndsWith(":memory:"))
        {
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            builder.Services.AddDbContext<NotesDbContext>(options => options.UseSqlite(connection));
            var db = new NotesDbContext(new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            builder.Services.AddDbContext<NotesDbContext>(options => options.UseSqlite(connectionString));
        }

        builder.Services
            .AddSingleton<JwtOptions>(jwt)
            .AddSingleton<DemoTokenService>()
            .AddScoped<TokenService>()
#if DEBUG
            .AddScoped<SeedAppSettingsUsers>()
#endif
            .AddHttpContextAccessor()
            .AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            })
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
#if DEBUG
                    // Time tolerance for the expiration of the token.
                    ClockSkew = TimeSpan.FromSeconds(1),
#else
                    ClockSkew = TimeSpan.FromSeconds(30),
#endif
                };
            });

        builder.Services
            .AddGraphQLServer()
            .AddNrgApiTypes()
            .AddAuthorization()
            .AddHttpRequestInterceptor((context, _, request, _) =>
            {
                request.SetGlobalState("ClaimsPrincipal", context.User);
                return ValueTask.CompletedTask;
            })
            .AddFiltering()
            .AddSorting()
            .ModifyCostOptions(option =>
            {
                option.MaxFieldCost = 10_000_000;
                option.MaxTypeCost = 10_000_000;
            });

        var app = builder.Build();

        app.UseCors();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGroup("/auth").MapAuthEndpoints();

        app.MapGet("hello", () => "Hello World!").AllowAnonymous();
        app.MapGet("secure-hello", () => "Secure Hello World!").RequireAuthorization();

        app.MapGraphQL()
            // Either go for auth on /graphql endpoint and add a rest login endpoint
            // or set auth for each endpoint except the mutation login method.
            // This way we can use the palyground without auth first and graphql always respond with 200 OK.
            //.RequireAuthorization()
            .WithOptions(e =>
            {
                // Nitro Options (GraphQL Playground)
                e.Enable = false;
                e.Title = "NRG-Drink Playground API";
                e.Document = "query { hello }";
                e.IncludeCookies = true;
#if DEBUG
                e.Enable = true;
#endif
            });

        if (app.Environment.IsDevelopment())
        {
            var seeder = app.Services.CreateScope().ServiceProvider.GetService<SeedAppSettingsUsers>();
            if (seeder is not null)
            {
                await seeder.SeedIdempotentAsync();
            }
        }

        app.RunWithGraphQLCommands(args);
    }
}
