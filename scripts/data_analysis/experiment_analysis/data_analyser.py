import os
from datetime import datetime, timedelta
from timedrift_adjuster import TimedriftAdjuster
from latex_writer import LatexWriter
import plot_maker
import json
import re
import pandas as pd
import time

data_dir=os.path.join(os.path.dirname(__file__),"experiment_data")

# Set to TRUE for faster collective analysis
skip_individual_analysis = False

# Use this dictionary if we want to make charts of special groupings
# Simply specify the name of your grouping as the dictionary-key and let the value be a list of names referring to experiments
# The value can also be a single string, in which case it will be treated as a regex
custom_groupings={
    "Cool Ones": [
        "Scaling 50K with GPUAccelerated",
        "Scaling 50K with TimeBucketedNeo4j",
        "Scaling 100K with ManyTablesPostgres",
    ],
    "AccuracyUnderLoadWithoutGPU": "^Accuracy under load((?!GPUAccelerated).)+$"
}

# Names in here must match excatly
sorting_order = [
    "Realistic Case",
    "Scaling 50K",
    "Scaling 100K",
    "Scaling 260k",
    "Scaling 1M",
    "Scaling 50K w. a. flights",
    "Worst-Case",
    "Accuracy under load",
    "Stress-test with recalc",
]

def custom_experiment_sorting_order(name):
    try:
        return sorting_order.index(os.path.basename(name))
    except:
        return 9999999 


def fix_name(name):
    return name.replace("Baseline", "Scaling 50K").replace(" (30258)", "").replace("while adding flights", "w. a. flights").replace("  ", " ")

def analyze_data(experiments):
    global skip_individual_analysis
    print(f"Found {len(experiments)} experiments to analyze")
    experimentType_datastore_map = dict()
    datastore_experiment_map = dict()
    weatherFrames = dict()
    flightFrames = dict()
    recalculationFrames = dict()
    lagFrames = dict()
    consumptionFrames = dict()
    flightConsumptionFrames = dict()
    experiment_runtime = dict()

    summary_analysis_path = os.path.join(os.path.dirname(__file__), "analysis_summary")
    if not os.path.exists(summary_analysis_path):
        os.makedirs(summary_analysis_path)
        os.makedirs(os.path.join(summary_analysis_path, "experiments"))
        os.makedirs(os.path.join(summary_analysis_path, "data-stores"))

    for experiment in experiments:
        dataset_path = os.path.join(data_dir, experiment)
        if not os.path.exists(dataset_path):
            raise Exception(f"Experiment data \"{experiment}\" not found")
        
        experiment_data = None
        with open(os.path.join(dataset_path, "metadata.json"), "r") as f:
            experiment_data = json.load(f)['experimentData']
        
        experiment_name = experiment_data['experimentRunDescription']
        experiment_name = fix_name(experiment_name)

        print(f"\n\nAnalyzing {experiment_name} with client-id: {experiment_data['clientId']}")

        experiment_data_store = experiment_data['dataStoreType']
        experiment_data_store_key = os.path.join("data-stores", experiment_data_store)
        if experiment_data_store_key in datastore_experiment_map:
            datastore_experiment_map[experiment_data_store_key].append(experiment_name)
        else:
            datastore_experiment_map[experiment_data_store_key] = [experiment_name]

        experiment_type_name = experiment_data['experiment']['name']

        experiment_type_name = fix_name(experiment_type_name)

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
        latency = experiment_data['latencyTest']['medianLatencyMs'] / 2 # Divide by 2 because latency round-trip
        adjuster = TimedriftAdjuster(baseTime, consumerTime, latency)
        print(f"Detected lag of {adjuster.time_drift.total_seconds():.2f} seconds")

        # Fix the ReceivedTimestamp by applying time-drift adjustment
        weatherDf["ReceivedTimestamp"] = weatherDf["ReceivedTimestamp"].apply(adjuster.get_adjusted_time)
        startTime = weatherDf["ReceivedTimestamp"][0]
        weatherDf["ReceivedSecondsAfterStart"] = weatherDf["ReceivedTimestamp"].apply(lambda x: (x - startTime))
        startTime = weatherDf["SentTimestamp"][0]
        weatherDf["SentSecondsAfterStart"] = weatherDf["SentTimestamp"].apply(lambda x: (x - startTime))
        flightDf["ReceivedTimestamp"] = flightDf["ReceivedTimestamp"].apply(adjuster.get_adjusted_time)

        # Commented out because most datasets contains no recieved flights
        if len(flightDf["ReceivedTimestamp"]) > 0:
            startTime = flightDf["ReceivedTimestamp"][0]
            flightDf["ReceivedSecondsAfterStart"] = flightDf["ReceivedTimestamp"].apply(lambda x: (x - startTime))
        recalculationDf["LagMs"] = recalculationDf["LagMs"].apply(adjuster.get_adjusted_lag)
        
        startTime = lagDf["Timestamp"][0]
        lagDf["TimestampSecondsAfterStart"] = lagDf["Timestamp"].apply(lambda x: (x - startTime))

        # Calculate consumption rates

        weatherConsumptionRate = weatherDf.groupby(pd.Grouper(key="ReceivedSecondsAfterStart",freq='s'))["WeatherId"].count()
        flightConsumptionRate = None
        fIndex = None
        if "ReceivedSecondsAfterStart" in flightDf:
            flightConsumptionRate = flightDf.groupby(pd.Grouper(key="ReceivedSecondsAfterStart",freq='s'))["FlightId"].count()
            fIndex = flightConsumptionRate.index

        # Save the adjusted frames so they can be used later
        weatherFrames[experiment_name] = weatherDf
        flightFrames[experiment_name] = flightDf
        recalculationFrames[experiment_name] = recalculationDf
        lagFrames[experiment_name] = lagDf
        consumptionFrames[experiment_name] = weatherConsumptionRate
        flightConsumptionFrames[experiment_name] = flightConsumptionRate

        # INDIVIDUAL ANALYSIS START
        if skip_individual_analysis:
            continue

        experimentTime = (datetime.fromisoformat(experiment_data['utcEndTime']) - datetime.fromisoformat(experiment_data['utcStartTime'])).total_seconds()
        expectedTime = (datetime.fromisoformat(experiment_data['experiment']['simulatedEndTime']) - datetime.fromisoformat(experiment_data['experiment']['simulatedStartTime'])).total_seconds()
        timeScale = int(experiment_data['experiment']['timeScale'])
        if timeScale > 0:
            expectedTime = expectedTime / timeScale
        else:
            expectedTime = 0

        expectedTime += 15 # The orchestrator always waits 15 seconds after an experiment before concluding it's done.
                           # This is due to delays with how RabbitMQ reports the consumer-lag.
        experiment_runtime[experiment_name] = (experimentTime, expectedTime)

        analysis_path = os.path.join(summary_analysis_path, "single_experiments", experiment_name)
        if not os.path.exists(analysis_path):
            os.makedirs(analysis_path)

        # Recalculation data
        recalculationDf["LagMs"].describe().to_csv(os.path.join(analysis_path, "recalculation_summary.csv"))
        plot_maker.make_recalculation_boxplot([recalculationDf["LagMs"]], [experiment_name], analysis_path)
        
        #Lag data
        lagDf[["WeatherLag", "FlightLag"]].describe().to_csv(os.path.join(analysis_path, "lag_summary.csv"))
        last_data_point = weatherDf["SentSecondsAfterStart"].iat[-1].total_seconds()
        plot_maker.make_lag_chart(lagDf["TimestampSecondsAfterStart"], lagDf["WeatherLag"], lagDf["FlightLag"], experiment_name, last_data_point, analysis_path)
        plot_maker.make_weather_lag_boxplot([lagDf["WeatherLag"]], [experiment_name], analysis_path)

        # Make consumption chart
        pd.DataFrame(removeZeroEntries(weatherConsumptionRate)).describe().to_csv(os.path.join(analysis_path, "weather_consumption.csv"))
        if not flightConsumptionRate is None:
            pd.DataFrame(removeZeroEntries(flightConsumptionRate)).describe().to_csv(os.path.join(analysis_path, "flight_consumption.csv"))
        
        plot_maker.make_consumption_chart(weatherConsumptionRate.index, weatherConsumptionRate, fIndex, flightConsumptionRate,  experiment_name, analysis_path)

    # INDIVIDUAL ANALYSIS DONE

    global custom_groupings

    # Start by making graphs grouped by data-store and experiment_type
    latex_count = 0
    for filter_map in [datastore_experiment_map, experimentType_datastore_map, custom_groupings]:
        latex_writer = LatexWriter()

        filter_keys = list(filter_map.keys())
        filter_keys = sorted(filter_keys, key=custom_experiment_sorting_order)

        for filter_item in filter_keys:
            experiment_names = filter_map[filter_item]

            filtering_lambda = lambda x: x[0] in experiment_names
            if isinstance(experiment_names, str):
                filtering_lambda = lambda x: len(re.findall(experiment_names, x[0])) > 0

            # Make filters
            recalcs_for_filter = dict(filter(filtering_lambda, recalculationFrames.items()))
            lag_for_filter = dict(filter(filtering_lambda, lagFrames.items()))
            consumption_for_filter = dict(filter(filtering_lambda, consumptionFrames.items()))
            flight_consumption_for_filter = dict(filter(filtering_lambda, flightConsumptionFrames.items()))
            runtime_for_filter = dict(filter(filtering_lambda, experiment_runtime.items()))
            
            make_collective_analysis(recalcs_for_filter, lag_for_filter, consumption_for_filter, flight_consumption_for_filter, runtime_for_filter, summary_analysis_path, filter_item)
            
            if not isinstance(experiment_names, str):
                latex_data_stores = []
                for i in range(len(experiment_names)):
                    if not experiment_names[i] in recalcs_for_filter:
                        print(f"Filtering for {experiment_names[i]} does not match any seen experiment. Is this an error?")
                        continue
                    flights_na = flight_consumption_for_filter[experiment_names[i]] is None
                    
                    latex_data_stores.append([
                        experiment_names[i], # name
                        recalcs_for_filter[experiment_names[i]]["LagMs"], #recalc
                        lag_for_filter[experiment_names[i]]["WeatherLag"],#weather_lag
                        "N/A" if flights_na else lag_for_filter[experiment_names[i]]["FlightLag"],#flight_lag
                        pd.DataFrame(removeZeroEntries(consumption_for_filter[experiment_names[i]])),#weather_rate
                        "N/A" if flights_na else pd.DataFrame(removeZeroEntries(flight_consumption_for_filter[experiment_names[i]])).median(),#flight_rate
                    ])
                    
                latex_writer.add_experiment(os.path.basename(filter_item), latex_data_stores)
        
        latex_writer.write_file(os.path.join(summary_analysis_path, f"report_{latex_count}.tex"))
        latex_count += 1

    # Make collective analysis for ALL frames
    make_collective_analysis(recalculationFrames, lagFrames, consumptionFrames, flightConsumptionFrames, experiment_runtime, summary_analysis_path)
    
def getColumns(frameDictionary, property):
    return list(map(lambda x: x[property],frameDictionary.values()))

def removeZeroEntries(frame):
    return list(filter(lambda x: x > 0, list(frame)))

def make_collective_analysis(recalcFrames, lagFrames, consumptionFrames, flightConsumptionFrames, runtimeFrames, output_dir, output_file=None):
    #Recalculation
    plot_maker.make_recalculation_boxplot(getColumns(recalcFrames, "LagMs"), recalcFrames.keys(), output_dir, output_file)

    #Max Lag
    max_weather_lag = list(map(max, getColumns(lagFrames, "WeatherLag")))
    max_flight_lag = list(map(max, getColumns(lagFrames, "FlightLag")))
    plot_maker.make_max_lag_chart(max_weather_lag, max_flight_lag, lagFrames.keys(), output_dir, output_file)
    plot_maker.make_max_lag_chart_weather(max_weather_lag, lagFrames.keys(), output_dir, output_file)
    plot_maker.make_weather_lag_boxplot(getColumns(lagFrames, "WeatherLag"), recalcFrames.keys(), output_dir, output_file)

    # Consumption rate
    consumptionIndicies = list(map(lambda x: x.index, consumptionFrames.values()))
    plot_maker.make_overlapping_consumption_chart(consumptionIndicies, list(consumptionFrames.values()), list(consumptionFrames.keys()), output_dir, output_file)
    
    filtered_consumption_weather = dict()
    for key, val in consumptionFrames.items():
        filtered_consumption_weather[key] = removeZeroEntries(val)
    plot_maker.make_consumption_boxplot(list(filtered_consumption_weather.values()), list(filtered_consumption_weather.keys()), output_dir, output_file)
    
    filtered_consumption_flights = dict()
    for key, val in flightConsumptionFrames.items():
        if not val is None:
            filtered_consumption_flights[key] = removeZeroEntries(val)
    if len(filtered_consumption_flights) > 0:
        plot_maker.make_flight_consumption_boxplot(list(filtered_consumption_flights.values()), list(filtered_consumption_flights.keys()), output_dir, output_file)

    # Runtime
    experimentTimes = getColumns(runtimeFrames, 0)
    experimentExpectedTimes = getColumns(runtimeFrames, 1)
    expectedTime = None
    if len(set(experimentExpectedTimes)) == 1:
        # Only set expected time if all the experiments we're comparing have the same expected time
        expectedTime = experimentExpectedTimes[0]

    plot_maker.make_completion_time_bar(experimentTimes, runtimeFrames.keys(), expectedTime, output_dir, output_file)

    
if __name__ == "__main__":
    start = time.time()
    analyze_data(os.listdir(data_dir))
    end = time.time()
    duration = timedelta(seconds=(end - start)) 
    print(f"\n\n == DONE in {duration} ==")