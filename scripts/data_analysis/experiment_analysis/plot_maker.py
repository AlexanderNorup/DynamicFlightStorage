import matplotlib.pyplot as plt
import matplotlib.dates as mdates
import numpy as np
import os

def breakArrayName(nameArray):
    return list(map(lambda x: x.replace("with ", "with\n"), nameArray))

def make_recalculation_boxplot(dataArray, nameArray, outputPath, chartName=None):
    fig, ax = plt.subplots()
    ax.boxplot(dataArray)
    ax.set_xticklabels(breakArrayName(nameArray))
    #ax.tick_params(axis='x', which='major', pad=-5)
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
    ax.set_xticklabels(breakArrayName(nameArray))
    #ax.tick_params(axis='x', which='major', pad=-5)
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

def make_lag_chart(time,weatherLag, flightLag, name, outputPath, chartName=None):
    fig, ax = plt.subplots()
    locator = mdates.AutoDateLocator(minticks=7, maxticks=10)
    formatter = mdates.ConciseDateFormatter(locator)
    ax.xaxis.set_major_locator(locator)
    ax.xaxis.set_major_formatter(formatter)
    ax.plot(time, weatherLag, label="weather")
    ax.plot(time, flightLag, label="flight")
    ax.legend(["Weather", "Flights"])
    ax.set_title(f"Consumer lag for {name}")
    ax.set_ylabel("# of messages waiting")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
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
    locator = mdates.AutoDateLocator(minticks=7, maxticks=10)
    formatter = mdates.ConciseDateFormatter(locator)
    ax.xaxis.set_major_locator(locator)
    ax.xaxis.set_major_formatter(formatter)
    ax.plot(time, weatherConsumption, label="Weather")
    ax.legend()
    ax.set_title(f"Consumption rate for {name}")
    ax.set_ylabel("# of weather events per second")
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
    fileName = "consumption_rate.pdf"
    if not chartName == None:
        fileName = chartName + "_consumption_rate.pdf"
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
    ax.set_xticks(x + width/2, labels=breakArrayName(nameArray))
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
    ax.set_xticks(x, labels=breakArrayName(nameArray))
    ax.grid(True,axis="y",linestyle='-', which='major', color='lightgrey',alpha=0.5)
    
    fileName = "max_consumer_lag_weather.pdf"
    if not chartName == None:
        fileName = chartName + "_max_consumerlag_weather.pdf"
    lag_path = os.path.join(outputPath, fileName)

    fig.autofmt_xdate() # Automatically rotates label so it can be read with multiple boxplots in same chart
    fig.savefig(lag_path)
    plt.close()
    
    print(f"Wrote {lag_path}")


