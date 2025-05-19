import os
import time
from datetime import datetime, timedelta
from timedrift_adjuster import TimedriftAdjuster
import pandas as pd
import json
from data_downloader import download_dir
from multiprocessing import Pool, cpu_count

should_skip_if_exists = True

def calculate_lag(experiment_name: str):
    global should_skip_if_exists
    experiment_path = os.path.join(download_dir, experiment_name)
    out_path = os.path.join(experiment_path, "lagLog.calculated.csv")
    
    if not os.path.exists(experiment_path):
        raise Exception(f"Experiment {experiment_name} not found at {experiment_path}")

    if should_skip_if_exists and os.path.exists(out_path):
        print(f"Skipping {experiment_name} as calculated file already exists => {out_path}")
        return

    print(f"Calculating precise lag for {experiment_name}")
    experiment_data = None
    with open(os.path.join(experiment_path, "metadata.json"), "r") as f:
        experiment_data = json.load(f)['experimentData']
    
    weatherDf = pd.read_csv(os.path.join(experiment_path, "weatherLog.csv"), parse_dates=['SentTimestamp', 'ReceivedTimestamp'])
    flightDf = pd.read_csv(os.path.join(experiment_path, "flightlog.csv"), parse_dates=['SentTimestamp', 'ReceivedTimestamp'])
    
    #Start by finding time-drift
    baseTime = weatherDf["SentTimestamp"][0]
    consumerTime = weatherDf["ReceivedTimestamp"][0]
    latency = experiment_data['latencyTest']['medianLatencyMs'] / 2 # Divide by 2 because latency round-trip
    adjuster = TimedriftAdjuster(baseTime, consumerTime, latency)
    print(f"Detected lag of {adjuster.time_drift.total_seconds():.2f} seconds")

    # Fix the ReceivedTimestamp by applying time-drift adjustment
    weatherDf["ReceivedTimestamp"] = weatherDf["ReceivedTimestamp"].apply(adjuster.get_adjusted_time)
    flightDf["ReceivedTimestamp"] = flightDf["ReceivedTimestamp"].apply(adjuster.get_adjusted_time)

    calculated_lag = {
        "Timestamp": [],
        "WeatherLag" : [],
        "FlightLag": []
    }

    def get_lag_at_point(df: pd.Series, recieved_time: datetime) -> int:
        # Binary searching for the earliest index greater than or equal to the time
        pos = df['ReceivedTimestamp'].searchsorted(recieved_time, side='left')
        if pos >= len(df['ReceivedTimestamp']):
            return 0 # Recieved_time is after last data-point. 0 Lag

        recieved_index = df.index[pos]
        lag = df["SentTimestamp"].where(lambda x: x < recieved_time).count() - recieved_index

        return lag

    total_length = len(weatherDf["ReceivedTimestamp"])
    for index, row in weatherDf.iterrows():
        if index % 1000 == 0:
            print(f"Calculating lag for {experiment_name}: {index}/{total_length} ({(index+1)/total_length*100:.2f}%)")
        weather_lag = get_lag_at_point(weatherDf, row['ReceivedTimestamp'])
        flight_lag = get_lag_at_point(flightDf, row['ReceivedTimestamp'])
        calculated_lag["Timestamp"].append(row["ReceivedTimestamp"].isoformat().replace("+00:00", "Z"))
        calculated_lag["WeatherLag"].append(weather_lag)
        calculated_lag["FlightLag"].append(flight_lag)

    print(f"Writing lag-file: {experiment_name}")
    calculated_lag_df = pd.DataFrame(calculated_lag)
    calculated_lag_df.to_csv(out_path, index=False)
    print(f"Calculated precise lag for {experiment_path} => {out_path}")

    # Make a quick plot for debugging
    # import plot_maker
    # startTime =  datetime.fromisoformat(calculated_lag_df["Timestamp"][0])
    # calculated_lag_df["TimestampSecondsAfterStart"] = calculated_lag_df["Timestamp"].apply(lambda x: (datetime.fromisoformat(x) - startTime))
    # last_data_point = None
    # plot_maker.make_lag_chart(calculated_lag_df["TimestampSecondsAfterStart"], calculated_lag_df["WeatherLag"], calculated_lag_df["FlightLag"], experiment_name, last_data_point, experiment_path)

if __name__  == "__main__":
    # calculate_lag("Scaling 50K while adding flights with BTreePostgres")
    experiments_to_process = os.listdir(download_dir)
    max_parallelism = cpu_count()
    pool = Pool(max_parallelism)
    print(f"Processing {len(experiments_to_process)} experiments in parallel with max parallelism={max_parallelism}")
    
    start = time.time()
    pool.map(calculate_lag, experiments_to_process)
    end = time.time()
    duration = timedelta(seconds=(end - start)) 
    print(f"\n == DONE in {duration} ==")
