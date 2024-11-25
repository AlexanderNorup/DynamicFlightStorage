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

        public DataCollectionContext(DbContextOptions<DataCollectionContext> context) : base(context)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExperimentResult>().HasMany(x => x.ClientResults).WithOne(x => x.ExperimentResult);
        }
    }
}
