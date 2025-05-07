
# Names in here must match excatly
sorting_order = [
    "Realistic Case (real time)",
    "Realistic Case",
    "Scaling 50K w. a. flights",
    "Scaling 50K",
    "Scaling 100K",
    "Scaling 260K",
    "Scaling 1M",
    "Accuracy under load",
    "Stress-test with recalc",
    "Worst-Case",
]

# Data-store names for overview table
data_store_names = [
    ("BTreePostgres", "OptimizedPostgreSQLDataStore.BTreePostgreSQLDataStore"),
    ("SpatialPostgres", "SpatialGISTPostgreSQL.SpatialPostgreSQLDatastore"),
    ("ManyTablesPostgres", "ManyTablesPostgreSQLDataStore.ManyTablesPostgreSQLDatastore"),
    ("RelationalNeo4j", "Neo4jDataStore.RelationalNeo4jDataStore"),
    ("TimeBucketedNeo4j (1 hour)", "Neo4jDataStore.TimeBucketedNeo4jDataStore(60min)"),
    ("TimeBucketedNeo4j (1 day)", "Neo4jDataStore.TimeBucketedNeo4jDataStore(1440min)"),
    ("GPUAccelerated", "GPUAcceleratedEventDataStore.CUDAEventDataStore")
]

def custom_experiment_sorting_order(name):
    try:
        return sorting_order.index(os.path.basename(name))
    except:
        return 9999999 


def fix_name(name):
    return name.replace("Baseline", "Scaling 50K").replace(" (30258)", "").replace("while adding flights", "w. a. flights").replace("  ", " ").replace("Scaling 260k", "Scaling 260K").replace("Worst-case", "Worst-Case")

def should_skip_experiment(name: str):
    if name.lower().startswith("260k maxspeed") or name.lower().startswith("100k maxspeed"):
        return True
    return False

def fix_name_if_datastore(name: str):
    global data_store_names
    for real_name, tech_name in data_store_names:
        if name.lower() == tech_name.lower():
            return real_name
    return name