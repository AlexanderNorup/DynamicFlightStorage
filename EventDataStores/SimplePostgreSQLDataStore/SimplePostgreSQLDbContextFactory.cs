using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Testcontainers.PostgreSql;

namespace SimplePostgreSQLDataStore
{
    internal class SimplePostgreSQLDbContextFactory : IDesignTimeDbContextFactory<SimplePostgreSQLDbContext>
    {
        public SimplePostgreSQLDbContext CreateDbContext(string[] args)
        {
            // Used for EF Core design-time tools for automatically generating the migrations
            // If we change the database schema:
            // 1. Change the files to the new schema
            // 2. Delete the migrations folder
            // 3. Run `dotnet ef migrations add InitialCreate`
            //      (Requires the ef-core tools to be installed with `dotnet tool install --global dotnet-ef`)
            // It will then generate the migrations folder with the new schema
            // The reason this is a bit wierd is that we're misuing the migrations feature
            // We just use it so we can automatically generate the tables.
            // Migrations assume that the previous migrations are present in the database already
            // That's just not the case for us because we use TestContainers in this funny way.

            var container = new PostgreSqlBuilder().Build();
            container.StartAsync().GetAwaiter().GetResult();
            var contextOptionsBuilder = new DbContextOptionsBuilder<SimplePostgreSQLDbContext>();
            contextOptionsBuilder.UseNpgsql(container.GetConnectionString());
            var context = new SimplePostgreSQLDbContext(contextOptionsBuilder.Options);
            return context;
        }
    }
}
