import matplotlib.ticker as ticker
import matplotlib.pyplot as plt
import matplotlib.dates as mdates
import numpy as np
from datetime import timedelta
import os

# Allows for pretty-priting a timedelata as x-values
def timedelta_formatter(x, pos=None):
    ms = x / 1e6
    td = timedelta(milliseconds=ms)
    return str(td)

def breakArrayName(nameArray):
    return list(map(lambda x: x.replace("with ", "with\n"), nameArray))

def make_recalculation_boxplot(dataArray, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    ax.boxplot(dataArray)
    ax.set_xticklabels(breakArrayName(nameArray), fontsize=8)
    ax.tick_params(axis='x', which='major', pad=-3)
    fig.subplots_adjust(bottom=0.65)
    ax.set_title("Recalculation lag")
    ax.set_ylabel("Time to respond (ms)")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)

    fileName = "recalculation_lag.pdf"
    if not chartName == None:
        fileName = chartName + "_recalc.pdf"
    lag_path = os.path.join(outputPath, fileName)
    
    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")

def make_weather_lag_boxplot(dataArray, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    ax.boxplot(dataArray)
    ax.set_xticklabels(breakArrayName(nameArray), fontsize=8)
    ax.tick_params(axis='x', which='major', pad=-3)
    fig.subplots_adjust(bottom=0.65)
    ax.set_title("Weather lag")
    ax.set_ylabel("# of weather events waiting")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)

    fileName = "weather_lag_box.pdf"
    if not chartName == None:
        fileName = chartName + "_weather_lag_box.pdf"
    lag_path = os.path.join(outputPath, fileName)
    
    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")

def make_lag_chart(time,weatherLag, flightLag, name, finishTime, outputPath, chartName=None):
    fig, ax = plt.subplots()
    formatter = ticker.FuncFormatter(timedelta_formatter)
    ax.xaxis.set_major_formatter(formatter)
    ax.plot(time, weatherLag, label="Weather")
    ax.plot(time, flightLag, label="Flight")
    ax.set_title(f"Consumer lag for {name}")
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
    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")

def make_consumption_chart(time, weatherConsumption, name, outputPath, chartName=None):
    fig, ax = plt.subplots()
    formatter = ticker.FuncFormatter(timedelta_formatter)
    ax.xaxis.set_major_formatter(formatter)
    ax.plot(time, weatherConsumption, label="Weather")
    ax.legend()
    ax.set_title(f"Consumption rate for {name}")
    ax.set_ylabel("# of weather events per second")
    ax.set_xlabel("Time after experiment start")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)

    fileName = "consumption_rate.pdf"
    if not chartName == None:
        fileName = chartName + "_consumption_rate.pdf"
    lag_path = os.path.join(outputPath, fileName)
    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
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
    ax.set_title(f"Weather Consumption rate")
    ax.set_ylabel("# of weather events per second")
    ax.set_xlabel("Time after experiment start")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
    fileName = "consumption_rates.pdf"
    if not chartName == None:
        fileName = chartName + "_consumption_rates.pdf"
    lag_path = os.path.join(outputPath, fileName)
    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")


def make_consumption_boxplot(dataArray, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    ax.boxplot(dataArray)
    ax.set_xticklabels(breakArrayName(nameArray), fontsize=8)
    ax.tick_params(axis='x', which='major', pad=-3)
    fig.subplots_adjust(bottom=0.65)
    ax.set_title("Weather Consumption rate")
    ax.set_ylabel("# of weather events per second")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)

    fileName = "consumption_rates_box.pdf"
    if not chartName == None:
        fileName = chartName + "_consumption_rates_box.pdf"
    lag_path = os.path.join(outputPath, fileName)
    
    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    print(f"Wrote {lag_path}")

def make_max_lag_chart(maxWeatherLag, maxFlightLag, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    x = np.arange(len(nameArray))  # the label locations
    width = 0.33  # the width of the bars

    bar = ax.bar(x, maxWeatherLag, width=width, label="Weather")
    ax.bar_label(bar, padding=3)
    bar = ax.bar(x + width, maxFlightLag, width=width, label="Flight")
    ax.bar_label(bar, padding=3)

    ax.legend(loc='upper left', ncols=2)
    ax.set_title("Maximum Consumer lag")
    ax.set_ylabel("max # of messages waiting")
    ax.set_xticks(x + width/2, labels=breakArrayName(nameArray), fontsize=8)
    ax.tick_params(axis='x', which='major', pad=-3)
    fig.subplots_adjust(bottom=0.65)
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
    fileName = "max_consumer_lag.pdf"
    if not chartName == None:
        fileName = chartName + "_max_consumerlag.pdf"
    lag_path = os.path.join(outputPath, fileName)

    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    
    print(f"Wrote {lag_path}")

def make_max_lag_chart_weather(maxWeatherLag, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    x = np.arange(len(nameArray))  # the label locations
    width = .5  # the width of the bars

    bar = ax.bar(x, maxWeatherLag, width=width, label="Weather")
    ax.bar_label(bar, padding=3)

    ax.legend(loc='upper left', ncols=2)
    ax.set_title("Maximum Consumer lag")
    ax.set_ylabel("max # of weather events waiting")
    ax.tick_params(axis='x', which='major', pad=-3)
    ax.set_xticks(x, labels=breakArrayName(nameArray), fontsize=8)
    fig.subplots_adjust(bottom=0.65)
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
    fileName = "max_consumer_lag_weather.pdf"
    if not chartName == None:
        fileName = chartName + "_max_consumerlag_weather.pdf"
    lag_path = os.path.join(outputPath, fileName)

    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    
    print(f"Wrote {lag_path}")


def make_completion_time_bar(completionTimes, nameArray, expectedFinishTime, outputPath, chartName=None):
    fig, ax = plt.subplots()
    x = np.arange(len(nameArray))  # the label locations
    width = .5  # the width of the bars

    bar = ax.bar(x, completionTimes, width=width)
    ax.bar_label(bar, padding=3, fmt=lambda x: f"{round(x)} s.")

    ax.set_title("Experiment time")
    ax.set_ylabel("total time to run experiment in seconds")
    ax.tick_params(axis='x', which='major', pad=-3)
    ax.set_xticks(x, labels=breakArrayName(nameArray), fontsize=8)
    fig.subplots_adjust(bottom=0.65)
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    if not expectedFinishTime == None:
        ax.plot([-0.5, len(nameArray) - 0.5], [expectedFinishTime,expectedFinishTime], color='green', linestyle='dashed', linewidth=2, label=f"Optimal finish time ({round(expectedFinishTime)} s.)")
        ax.legend()

    fileName = "time.pdf"
    if not chartName == None:
        fileName = chartName + "_time.pdf"
    lag_path = os.path.join(outputPath, fileName)

    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    
    print(f"Wrote {lag_path}")
