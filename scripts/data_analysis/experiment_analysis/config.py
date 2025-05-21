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

chart_sort_order = [] # Dynamic array containing first all data store names in order then experiments in order
for data_store, _ in data_store_names:
    chart_sort_order.append(data_store)

for experiment in sorting_order:
    chart_sort_order.append(experiment)

def chart_sorting_order(name: str):
    global chart_sort_order
    l_name = name.lower().replace("\n", " ").strip()
    for i in reversed(range(len(chart_sort_order))): # Reverse order due to baseline 2x
        if l_name.startswith(chart_sort_order[i].lower()):
            return i
    return 9999999

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
    ("Realistic Case (real time)", "Baseline"),
    ("Realistic Case", "Baseline 2x"),
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