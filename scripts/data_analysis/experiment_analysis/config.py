import os
# Names in here must match excatly
sorting_order = [
    "Baseline",
    "Baseline 2x",
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

name_replacements = [
    ("Baseline", "Scaling 50K"),
    (" (30258)", ""),
    ("while adding flights", "w. a. flights"),
    ("Scaling 260k", "Scaling 260K"),
    ("Worst-case", "Worst-Case"),
    ("Realistic Case (real time)", "Baseline 2x"),
    ("Realistic Case", "Baseline"),
    ("  ", " ")
]

def fix_name(name: str):
    global name_replacements
    out = name
    for (search, replace) in name_replacements:
        out = out.replace(search, replace)
    return out

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