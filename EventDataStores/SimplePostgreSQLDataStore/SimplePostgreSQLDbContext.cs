using Microsoft.EntityFrameworkCore;
using SimplePostgreSQLDataStore.DbEntities;

namespace SimplePostgreSQLDataStore
{
    internal class SimplePostgreSQLDbContext : DbContext
    {
        public DbSet<FlightEntity> Flights { get; set; }
        public DbSet<AirportEntity> Airports { get; set; }

        public SimplePostgreSQLDbContext(DbContextOptions<SimplePostgreSQLDbContext> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FlightEntity>()
                .HasMany(f => f.Airports)
                .WithOne(a => a.FlightEntity)
                .HasForeignKey(x => x.FlightEntityId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            modelBuilder.Entity<FlightEntity>()
                .Navigation(x => x.Airports).AutoInclude(); // We always want to fetch all airports when we do

            modelBuilder.Entity<AirportEntity>()
                .Navigation(x => x.FlightEntity)
                .AutoInclude();
        }
    }
}
