using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests
{
    /// <summary>
    /// Runs the real API in memory against its own in-memory SQLite database,
    /// with all migrations applied.
    /// </summary>
    public class StargateApiFactory : WebApplicationFactory<Program>
    {
        // A uniquely named shared-cache database lets every DbContext (and Dapper,
        // through the context's connection) see the same data. SQLite deletes an
        // in-memory database when its last connection closes, so one stays open.
        private readonly string _connectionString = $"Data Source=StargateTests-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _keepAliveConnection;

        public StargateApiFactory()
        {
            _keepAliveConnection = new SqliteConnection(_connectionString);
            _keepAliveConnection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<StargateContext>>();
                services.AddDbContext<StargateContext>(options => options.UseSqlite(_connectionString));
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            using (var scope = host.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<StargateContext>().Database.Migrate();
            }

            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _keepAliveConnection.Dispose();
            }
        }
    }
}
