using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using E2ETests_Demo.Data;
using E2ETests_Demo.Models;
using System;
using System.Linq;

//depends on Microsoft.EntityFrameworkCore.Sqlite import

namespace CloudTests
{
    public class SqliteTestFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        public SqliteTestFixture()
        {
            // Create and open an in-memory SQLite connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Build the service provider
            var services = new ServiceCollection();

            // Add Identity services
            services.AddDefaultIdentity<IdentityUser>()
                    .AddEntityFrameworkStores<ApplicationDbContext>();

            // Add a DbContext using SQLite
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            ServiceProvider = services.BuildServiceProvider();

            // Create the schema and seed data
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Ensure database is created
                db.Database.EnsureCreated();

                // Seed test data
                //SeedTestData(db);
            }
        }

        public IServiceProvider ServiceProvider { get; }

      
        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}