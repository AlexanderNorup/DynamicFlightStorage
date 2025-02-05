using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities;
using Microsoft.EntityFrameworkCore;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection
{
    public class DataCollectionContext : DbContext
    {
        public DbSet<Experiment> Experiments { get; set; }
        public DbSet<ExperimentResult> ExperimentResults { get; set; }
        public DbSet<ExperimentClientResult> ExperimentClientResults { get; set; }
        public DbSet<LatencyTestResult> LatencyTestResults { get; set; }

        public DbSet<FlightEventLog> FlightEventLogs { get; set; }
        public DbSet<WeatherEventLog> WeatherEventLogs { get; set; }
        public DbSet<RecalculationEventLog> RecalculationEventLogs { get; set; }

        public DataCollectionContext(DbContextOptions<DataCollectionContext> context) : base(context)
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Experiment>().HasMany(x => x.ExperimentResults).WithOne(x => x.Experiment);
            modelBuilder.Entity<ExperimentResult>().HasMany(x => x.ClientResults).WithOne(x => x.ExperimentResult);
        }
    }
}
