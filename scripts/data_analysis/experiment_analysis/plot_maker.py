import matplotlib.ticker as ticker
import matplotlib.pyplot as plt
import numpy as np
from datetime import timedelta
from config import chart_sorting_order
import os

plt.rcParams["figure.subplot.left"] = 0.15
plt.rcParams["figure.subplot.right"] = 0.98
DefaultBottom = 0.2

# Allows for pretty-priting a timedelata as x-values
def timedelta_formatter(x, pos=None):
    ms = x / 1e6
    td = timedelta(milliseconds=ms)
    return str(td)

def sort_arrays_by_base(base_array, arr1, arr2=None):
    if arr2 is None:
        arr2 = [None] * len(base_array)
    
    if len(base_array) != len(arr1):
        raise Exception("Base_array is not same length as array 1")
    
    combined = []
    
    for i in range(len(base_array)):
        combined.append((base_array[i],arr1[i],arr2[i]))
    
    combined_sorted = sorted(combined, key=lambda x: chart_sorting_order(x[0]))
    
    sorted_base, sorted_arr1, sorted_arr2 = [], [], []

    for base, a1, a2 in combined_sorted:
        sorted_base.append(base)
        sorted_arr1.append(a1)
        sorted_arr2.append(a2)

    return (sorted_base, sorted_arr1, sorted_arr2)



def format_name_array(names: list[str]) -> tuple[str, list[str]]:
    # Old behaviour
    default_bahavior = lambda _names: (None, list(map(lambda x: x.replace("with ", "\n").replace(" (", "\n("), _names)))
    
    single_datastore_name = None
    single_experiment_name = None
    detected_datastores = []
    detected_experiments = []
    for name in names:
        if " with " in name:
            split_name = name.split(" with ")
            found_experiment = " with ".join(split_name[0:len(split_name) -1 ]).strip()
            found_datastore = split_name[len(split_name) - 1].replace(" (", "\n(").strip()
            detected_experiments.append(found_experiment)
            detected_datastores.append(found_datastore)

            if single_datastore_name is None:
                single_datastore_name = found_datastore
            elif single_datastore_name != found_datastore:
                single_datastore_name = False

            if single_experiment_name is None:
                single_experiment_name = found_experiment
            elif single_experiment_name != found_experiment:
                single_experiment_name = False
            
            if single_experiment_name is False and single_datastore_name is False:
                # This grouping contains mixed data_stores and experiments. We must show the entire name
                return default_bahavior(names)
        else:
            # This does not follow the "[Experiment] with [data-store]" naming convension
            return default_bahavior(names)
    
    if len(detected_datastores) != len(names):
        # This shouldn't happen.
        print("Unexpected state when formatting name array for plots. Output array different length than input array")
        return default_bahavior(names)
    
    if single_datastore_name is not False:
        return (single_datastore_name, detected_experiments)
    elif single_experiment_name is not False:
        return (single_experiment_name, detected_datastores)
    
    print("Unexpected state when formatting name array for plots. Detected mixed types, but did not exit early.")
    print("Detected datastores:", detected_datastores)
    print("Detected experiments:", detected_experiments)
    return default_bahavior(names)

def make_recalculation_boxplot(dataArray_, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    grouping, xticks_ = format_name_array(nameArray)
    xticks, dataArray, _ = sort_arrays_by_base(xticks_, dataArray_)
    ax.boxplot(dataArray)
    if grouping is not None:
        fig.text(0.5, 0.9, grouping, horizontalalignment="center")
    ax.set_xticklabels(xticks, fontsize=8)

    ax.tick_params(axis='x', which='major', pad=-3)
    fig.subplots_adjust(bottom=0.65)
    fig.suptitle("Recalculation lag", y=0.98, fontsize=16)
    
    ax.set_ylabel("Time to respond (ms)")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)

    fileName = "recalculation_lag.pdf"
    if not chartName == None:
        fileName = chartName + "_recalc.pdf"
    lag_path = os.path.join(outputPath, fileName)
    
    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart

    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")

def make_weather_lag_boxplot(dataArray_, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    grouping, xticks_ = format_name_array(nameArray)
    xticks, dataArray, _ = sort_arrays_by_base(xticks_, dataArray_)
    ax.boxplot(dataArray)
    if grouping is not None:
        fig.text(0.5, 0.9, grouping, horizontalalignment="center")
    ax.set_xticklabels(xticks, fontsize=8)
    ax.tick_params(axis='x', which='major', pad=-3)
    fig.subplots_adjust(bottom=0.65)
    fig.suptitle("Weather lag", y=0.98, fontsize=16)
    ax.set_ylabel("# of weather events waiting")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)

    fileName = "weather_lag_box.pdf"
    if not chartName == None:
        fileName = chartName + "_weather_lag_box.pdf"
    lag_path = os.path.join(outputPath, fileName)
    
    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")

def make_lag_chart(time,weatherLag, flightLag, name, finishTime, outputPath, chartName=None):
    fig, ax = plt.subplots()
    formatter = ticker.FuncFormatter(timedelta_formatter)
    ax.xaxis.set_major_formatter(formatter)
    ax.plot(time, weatherLag, label="Weather")
    ax.plot(time, flightLag, label="Flight")
    fig.suptitle(f"Consumer lag for {name}", y=0.96, fontsize=12)
    ax.set_ylabel("# of messages waiting")
    ax.set_xlabel("Time after experiment start")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
    if not finishTime == None:
        yticks = ax.get_yticks()
        scale = yticks[2] / 5
        finishTimeNs = finishTime * 1e9
        ax.plot([finishTimeNs, finishTimeNs], [-scale,max(max(weatherLag),max(flightLag)) + scale], color='green', linestyle='dashed', linewidth=2, label=f"Publish stop")
    
    ax.legend()

    fileName = "consumer_lag.pdf"
    if not chartName == None:
        fileName = chartName + "_consumerlag.pdf"
    lag_path = os.path.join(outputPath, fileName)
    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")

def make_consumption_chart(time, weatherConsumption, f_time, flightConsumption, name, outputPath, chartName=None):
    fig, ax = plt.subplots()
    formatter = ticker.FuncFormatter(timedelta_formatter)
    ax.xaxis.set_major_formatter(formatter)
    ax.plot(time, weatherConsumption, label="Weather")
    if not flightConsumption is None:
        ax.plot(f_time, flightConsumption, label="Flight")
    ax.legend()
    fig.suptitle(f"Consumption rate for {name}", y=0.96, fontsize=12)
    ax.set_ylabel("# of events per second")
    ax.set_xlabel("Time after experiment start")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)

    fileName = "consumption_rate.pdf"
    if not chartName == None:
        fileName = chartName + "_consumption_rate.pdf"
    lag_path = os.path.join(outputPath, fileName)
    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")
    
def make_overlapping_consumption_chart(times, weatherConsumptions, names, outputPath, chartName=None):
    fig, ax = plt.subplots()
    formatter = ticker.FuncFormatter(timedelta_formatter)
    ax.xaxis.set_major_formatter(formatter)
    for i in range(len(times)):
        ax.plot(times[i], weatherConsumptions[i], label=names[i])
    ax.legend()
    fig.suptitle(f"Weather Consumption rate", y=0.96, fontsize=16)
    ax.set_ylabel("# of weather events per second")
    ax.set_xlabel("Time after experiment start")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
    fileName = "consumption_rates.pdf"
    if not chartName == None:
        fileName = chartName + "_consumption_rates.pdf"
    lag_path = os.path.join(outputPath, fileName)
    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")


def make_consumption_boxplot(dataArray_, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    grouping, xticks_ = format_name_array(nameArray)
    xticks, dataArray, _ = sort_arrays_by_base(xticks_, dataArray_)
    ax.boxplot(dataArray)
    if grouping is not None:
        fig.text(0.5, 0.9, grouping, horizontalalignment="center")
    ax.set_xticklabels(xticks, fontsize=8)
    ax.tick_params(axis='x', which='major', pad=-3)
    fig.subplots_adjust(bottom=0.65)
    fig.suptitle("Weather Consumption rate", y=0.98, fontsize=16)
    ax.set_ylabel("# of weather events consumed per second")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)

    fileName = "consumption_rates_box.pdf"
    if not chartName == None:
        fileName = chartName + "_consumption_rates_box.pdf"
    lag_path = os.path.join(outputPath, fileName)

    fig.text(1,1.02, "0-values when no weather\nwas injected removed", fontsize=8,
            horizontalalignment='right',
            verticalalignment='bottom',
            transform=ax.transAxes)

    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")


def make_flight_consumption_boxplot(dataArray_, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    grouping, xticks_ = format_name_array(nameArray)
    xticks, dataArray, _ = sort_arrays_by_base(xticks_, dataArray_)
    ax.boxplot(dataArray)
    if grouping is not None:
        fig.text(0.5, 0.9, grouping, horizontalalignment="center")
    ax.set_xticklabels(xticks, fontsize=8)
    ax.tick_params(axis='x', which='major', pad=-3)
    fig.subplots_adjust(bottom=0.65)
    fig.suptitle("Flight Consumption rate", y=0.98, fontsize=16)
    ax.set_ylabel("# of flight events consumed per second")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)

    fileName = "flight_consumption_rates_box.pdf"
    if not chartName == None:
        fileName = chartName + "_flight_consumption_rates_box.pdf"
    lag_path = os.path.join(outputPath, fileName)
    
    fig.text(1,1.02, "0-values when no flights\nwere injected removed", fontsize=8,
            horizontalalignment='right',
            verticalalignment='bottom',
            transform=ax.transAxes)

    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")

def make_max_lag_chart(maxWeatherLag_, maxFlightLag_, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    x = np.arange(len(nameArray))  # the label locations
    width = 0.33  # the width of the bars

    grouping, xticks_ = format_name_array(nameArray)
    xticks, maxWeatherLag, maxFlightLag = sort_arrays_by_base(xticks_, maxWeatherLag_, maxFlightLag_)

    bar = ax.bar(x, maxWeatherLag, width=width, label="Weather")
    ax.bar_label(bar, padding=3)
    bar = ax.bar(x + width, maxFlightLag, width=width, label="Flight")
    ax.bar_label(bar, padding=3)

    ax.legend(loc='upper left', ncols=2)
    fig.suptitle("Maximum Consumer lag", y=0.98, fontsize=16)
    ax.set_ylabel("max # of messages waiting")
    if grouping is not None:
        fig.text(0.5, 0.9, grouping, horizontalalignment="center")
    ax.set_xticks(x + width/2, labels=xticks, fontsize=8)
    ax.tick_params(axis='x', which='major', pad=-3)
    fig.subplots_adjust(bottom=0.65)
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
    fileName = "max_consumer_lag.pdf"
    if not chartName == None:
        fileName = chartName + "_max_consumerlag.pdf"
    lag_path = os.path.join(outputPath, fileName)

    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    
    print(f"Wrote {lag_path}")

def make_max_lag_chart_weather(maxWeatherLag_, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    x = np.arange(len(nameArray))  # the label locations
    width = .5  # the width of the bars

    grouping, xticks_ = format_name_array(nameArray)
    xticks, maxWeatherLag, _ = sort_arrays_by_base(xticks_, maxWeatherLag_)

    bar = ax.bar(x, maxWeatherLag, width=width, label="Weather")
    ax.bar_label(bar, padding=3)

    ax.legend(loc='upper left', ncols=2)
    fig.suptitle("Maximum Consumer lag", y=0.98, fontsize=16)
    ax.set_ylabel("max # of weather events waiting")
    ax.tick_params(axis='x', which='major', pad=-3)
    if grouping is not None:
        fig.text(0.5, 0.9, grouping, horizontalalignment="center")
    ax.set_xticks(x, labels=xticks, fontsize=8)
    fig.subplots_adjust(bottom=0.65)
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
    fileName = "max_consumer_lag_weather.pdf"
    if not chartName == None:
        fileName = chartName + "_max_consumerlag_weather.pdf"
    lag_path = os.path.join(outputPath, fileName)

    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    
    print(f"Wrote {lag_path}")


def make_completion_time_bar(completionTimes_, nameArray, expectedFinishTime, outputPath, chartName=None):
    fig, ax = plt.subplots()
    x = np.arange(len(nameArray))  # the label locations
    width = .5  # the width of the bars

    grouping, xticks_ = format_name_array(nameArray)
    xticks, completionTimes, _ = sort_arrays_by_base(xticks_, completionTimes_)

    bar = ax.bar(x, completionTimes, width=width)
    ax.bar_label(bar, padding=3, fmt=lambda x: f"{round(x)} s.")

    fig.suptitle("Experiment time", y=0.98, fontsize=16)
    ax.set_ylabel("total time to run experiment in seconds")
    ax.tick_params(axis='x', which='major', pad=-3)
    if grouping is not None:
        fig.text(0.5, 0.9, grouping, horizontalalignment="center")
    ax.set_xticks(x, labels=xticks, fontsize=8)
    fig.subplots_adjust(bottom=0.65)
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    if not expectedFinishTime == None:
        ax.plot([-0.5, len(nameArray) - 0.5], [expectedFinishTime,expectedFinishTime], color='green', linestyle='dashed', linewidth=2, label=f"Optimal finish time ({round(expectedFinishTime)} s.)")
        ax.legend()

    fileName = "time.pdf"
    if not chartName == None:
        fileName = chartName + "_time.pdf"
    lag_path = os.path.join(outputPath, fileName)

    fig.autofmt_xdate(bottom=DefaultBottom) # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    
    print(f"Wrote {lag_path}")
