import os
from datetime import datetime
from timedrift_adjuster import TimedriftAdjuster
import plot_maker
import json
import pandas as pd
import matplotlib.pyplot as plt

data_dir=os.path.join(os.path.dirname(__file__),"experiment_data")

def analyze_data(experiments):
    print(f"Found {len(experiments)} experiments to analyze")
    experimentType_datastore_map = dict()
    datastore_experiment_map = dict()
    weatherFrames = dict()
    flightFrames = dict()
    recalculationFrames = dict()
    lagFrames = dict()
    for experiment in experiments:
        dataset_path = os.path.join(data_dir, experiment)
        if not os.path.exists(dataset_path):
            raise Exception(f"Experiment data \"{experiment}\" not found")
        
        experiment_data = None
        with open(os.path.join(dataset_path, "metadata.json"), "r") as f:
            experiment_data = json.load(f)['experimentData']
        
        experiment_name = experiment_data['experimentRunDescription']
        experiment_name = experiment_name.replace("Baseline", "Scaling 50K")

        print(f"\n\nAnalyzing {experiment_name} with client-id: {experiment_data['clientId']}")

        experiment_data_store = experiment_data['dataStoreType']
        experiment_data_store_key = os.path.join("data-stores", experiment_data_store)
        if experiment_data_store_key in datastore_experiment_map:
            datastore_experiment_map[experiment_data_store_key].append(experiment_name)
        else:
            datastore_experiment_map[experiment_data_store_key] = [experiment_name]

        experiment_type_name = experiment_data['experiment']['name']

        experiment_type_name = experiment_type_name.replace("Baseline", "Scaling 50K")

        experiment_type_name_key = os.path.join("experiments", experiment_type_name)
        if experiment_type_name_key in experimentType_datastore_map:
            experimentType_datastore_map[experiment_type_name_key].append(experiment_name)
        else:
            experimentType_datastore_map[experiment_type_name_key] = [experiment_name]

        weatherDf = pd.read_csv(os.path.join(dataset_path, "weatherLog.csv"), parse_dates=['SentTimestamp', 'ReceivedTimestamp'])
        flightDf = pd.read_csv(os.path.join(dataset_path, "flightLog.csv"), parse_dates=['SentTimestamp', 'ReceivedTimestamp'])
        recalculationDf = pd.read_csv(os.path.join(dataset_path, "recalculationLog.csv"), parse_dates=['UtcTimeStamp'])
        lagDf = pd.read_csv(os.path.join(dataset_path, "lagLog.csv"), parse_dates=['Timestamp'])
        
        #Start by finding time-drift
        baseTime = weatherDf["SentTimestamp"][0]
        consumerTime = weatherDf["ReceivedTimestamp"][0]
        latency = experiment_data['latencyTest']['medianLatencyMs']
        adjuster = TimedriftAdjuster(baseTime, consumerTime, latency)
        print(f"Detected lag of {adjuster.time_drift.total_seconds():.2f} seconds")

        # Fix the ReceivedTimestamp by applying time-drift adjustment
        weatherDf["ReceivedTimestamp"] = weatherDf["ReceivedTimestamp"].apply(adjuster.get_adjusted_time)
        flightDf["ReceivedTimestamp"] = flightDf["ReceivedTimestamp"].apply(adjuster.get_adjusted_time)
        recalculationDf["LagMs"] = recalculationDf["LagMs"].apply(adjuster.get_adjusted_lag)

        # Save the adjusted frames so they can be used later
        weatherFrames[experiment_name] = weatherDf
        flightFrames[experiment_name] = flightDf
        recalculationFrames[experiment_name] = recalculationDf
        lagFrames[experiment_name] = lagDf

        analysis_path = os.path.join(dataset_path, "analysis")
        if not os.path.exists(analysis_path):
            os.makedirs(analysis_path)

        # Recalculation data
        recalculationDf["LagMs"].describe().to_csv(os.path.join(analysis_path, "recalculation_summary.csv"))
        plot_maker.make_recalculation_boxplot([recalculationDf["LagMs"]], [experiment_name], analysis_path)

        #Lag data
        lagDf[["WeatherLag", "FlightLag"]].describe().to_csv(os.path.join(analysis_path, "lag_summary.csv"))
        plot_maker.make_lag_chart(lagDf["Timestamp"], lagDf["WeatherLag"], lagDf["FlightLag"], experiment_name, analysis_path)
 

    # INDIVIDUAL ANALYSIS DONE

    summary_analysis_path = os.path.join(os.path.dirname(__file__), "analysis_summary")
    if not os.path.exists(summary_analysis_path):
        os.makedirs(summary_analysis_path)
        os.makedirs(os.path.join(summary_analysis_path, "experiments"))
        os.makedirs(os.path.join(summary_analysis_path, "data-stores"))

    # Start by making graphs grouped by data-store and experiment_type
    for filter_map in [datastore_experiment_map, experimentType_datastore_map]:
        for filter_item in filter_map:
            experiment_names = filter_map[filter_item]

            # Recalculation boxplot
            recalcs_for_filter = dict(filter(lambda x: x[0] in experiment_names, recalculationFrames.items()))

            # plot_maker.make_recalculation_boxplot(getColumns(recalcs_for_filter, "LagMs"), recalcs_for_filter.keys(), summary_analysis_path, filter_item + "_recalc.pdf")

            # Max Lag
            lag_for_filter = dict(filter(lambda x: x[0] in experiment_names, lagFrames.items()))
            # max_weather_lag = list(map(max, getColumns(lag_for_filter, "WeatherLag")))
            # max_flight_lag = list(map(max, getColumns(lag_for_filter, "FlightLag")))
            # plot_maker.make_max_lag_chart(max_weather_lag, max_flight_lag, lag_for_filter.keys(), summary_analysis_path, filter_item + "_maxlag.pdf")

            make_collective_analysis(recalcs_for_filter, lag_for_filter, summary_analysis_path, filter_item)

    # Make collective recalculaiton boxplot
    #plot_maker.make_recalculation_boxplot(getColumns(recalculationFrames, "LagMs"), recalculationFrames.keys(), summary_analysis_path)
    make_collective_analysis(recalculationFrames, lagFrames, summary_analysis_path)
    
def getColumns(frameDictionary, property):
    return list(map(lambda x: x[property],frameDictionary.values()))

def make_collective_analysis(recalcFrames, lagFrames, output_dir, output_file=None):
    #Recalculation
    plot_maker.make_recalculation_boxplot(getColumns(recalcFrames, "LagMs"), recalcFrames.keys(), output_dir, output_file)

    #Max Lag
    max_weather_lag = list(map(max, getColumns(lagFrames, "WeatherLag")))
    max_flight_lag = list(map(max, getColumns(lagFrames, "FlightLag")))
    plot_maker.make_max_lag_chart(max_weather_lag, max_flight_lag, lagFrames.keys(), output_dir, output_file)
    plot_maker.make_max_lag_chart_weather(max_weather_lag, lagFrames.keys(), output_dir, output_file)
    
if __name__ == "__main__":
    analyze_data(os.listdir(data_dir))