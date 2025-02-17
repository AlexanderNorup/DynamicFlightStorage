import json, os
from datetime import datetime, timedelta
import matplotlib.pyplot as plt
import pandas as pd
import matplotlib.dates as mdates
import pytz

# Adjust as needed
#metar_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/metar'
#taf_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/taf'
#flight_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/flights'

flight_dir = '/home/sebastian/Desktop/thesis/2024_10_10_flights/'

flight_df = pd.DataFrame()

def load_json():
    global flight_df
    flight_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/flights/'

    flight_errors = 0

    # Get date departure for each flight
    flight_files = [filename for filename in os.listdir(flight_dir)]

    flight_data = []

    for file_name in flight_files:
        file_path = os.path.join(flight_dir, file_name)
        with open(file_path) as json_file:
            data = json.load(json_file)
            try:
                flight_data.append({
                    'ScheduledTimeOfDeparture': datetime.fromisoformat(data['ScheduledTimeOfDeparture']).astimezone(pytz.utc),
                    'DatePlanned': datetime.fromisoformat(data['DatePlanned']).astimezone(pytz.utc),
                    'DepartureAirport': data.get('DepartureAirport', None)
                })
            except KeyError:
                flight_errors += 1
                continue

    flight_df = pd.DataFrame(flight_data)
    flight_df['DateHourDeparture'] = flight_df['ScheduledTimeOfDeparture'].dt.strftime('%Y-%m-%d %H')
    flight_df['DateDayDeparture'] = flight_df['ScheduledTimeOfDeparture'].dt.strftime('%Y-%m-%d')


def load_csv():
    global flight_df
    flight_dir = '/home/sebastian/Desktop/thesis/Real_flights.csv'
    # Read from the CSV file
    flight_df = pd.read_csv(flight_dir, parse_dates=['Takeoff_Time'])
    flight_df['DateHourDeparture'] = flight_df['Takeoff_Time'].dt.strftime('%Y-%m-%d %H')
    flight_df['DateDayDeparture'] = flight_df['Takeoff_Time'].dt.strftime('%Y-%m-%d')
    flight_df['HourDeparture'] = flight_df['Takeoff_Time'].dt.strftime('%H')
    print(flight_df.head())
    print(f"Flight data departure ranges from {flight_df['DateHourDeparture'].min()} to {flight_df['DateHourDeparture'].max()}")


    # Filtering to only be concerned with 23-05-10 until 23-06-07
    start_date = '2023-05-10'
    end_date = '2023-06-07'
    mask = (flight_df['Takeoff_Time'] >= start_date) & (flight_df['Takeoff_Time'] <= end_date)
    flight_df = flight_df.loc[mask]
    # Filter out flights on the days 2023-05-23 and 2023-05-24 (anomalies in data)
    exclude_dates = ['2023-05-23', '2023-05-24']
    flight_df = flight_df[~flight_df['DateDayDeparture'].isin(exclude_dates)]


def stats_overall_departuredate():
    global flight_df
    f_count_hour = flight_df['DateHourDeparture'].value_counts().sort_index()
    #f_count_hour = flight_df['HourDeparture'].value_counts().sort_index()
    # f_count_day = flight_df['DateDayDeparture'].value_counts().sort_index()
    # f_full_timeline = pd.date_range(start=start_date, end=end_date, freq='h').strftime('%Y-%m-%d %H')

    min_flights = f_count_hour.min()
    max_flights = f_count_hour.max()
    mean_flights = f_count_hour.mean()
    median_flights = f_count_hour.median()

    mean_flights_per_hour = f_count_hour.groupby(f_count_hour.index.str[-2:]).mean()
    min_flights_per_hour = f_count_hour.groupby(f_count_hour.index.str[-2:]).min()
    max_flights_per_hour = f_count_hour.groupby(f_count_hour.index.str[-2:]).max()
    median_flights_per_hour = f_count_hour.groupby(f_count_hour.index.str[-2:]).median()
    std_dev_flights_per_hour = f_count_hour.groupby(f_count_hour.index.str[-2:]).std()

    for hour in mean_flights_per_hour.index:
        print(f"Hour {hour}:00 - Mean: {mean_flights_per_hour[hour]:.2f}, Min: {min_flights_per_hour[hour]}, Max: {max_flights_per_hour[hour]}, Median: {median_flights_per_hour[hour]}, Std Dev: {std_dev_flights_per_hour[hour]:.2f}")

    print(f"Min flights per hour: {min_flights}")
    print(f"Max flights per hour: {max_flights}")
    print(f"Mean flights per hour: {mean_flights}")
    print(f"Median flights per hour: {median_flights}")

    plt.figure(figsize=(12, 6))
    f_count_hour.plot(kind='bar', color='skyblue')
    plt.xlabel('Hour')
    plt.ylabel('Number of flights')
    plt.title('Number of flights per Hour')
    plt.xticks(rotation=90)

    
    # Make every tick that is at 00 time bold
    '''new_ticks = []
    for i, tick in enumerate(plt.gca().get_xticklabels()):
        if ' 00' in tick.get_text():
            tick.set_fontweight('bold')
        if not (' 00' in tick.get_text() or ' 12' in tick.get_text()):
            tick.set_visible(False)
        new_ticks.append(tick)
    plt.gca().set_xticklabels(new_ticks)'''
    plt.tight_layout()
    plt.show()


def stats_overall_departuredate_us_eu():
    global flight_df
    flight_df_eu = flight_df[flight_df['Origin_ICAO'].str.startswith(('E', 'L'))]
    flight_df_us = flight_df[flight_df['Origin_ICAO'].str.startswith(('K'))]

    f_count_hour_eu = flight_df_eu['DateHourDeparture'].value_counts().sort_index()
    f_count_hour_us = flight_df_us['DateHourDeparture'].value_counts().sort_index()
    f_full_timeline = pd.date_range(start=start_date, end=end_date, freq='h').strftime('%Y-%m-%d %H')
    f_count_hour_eu = f_count_hour_eu.reindex(f_full_timeline, fill_value=0)
    f_count_hour_us = f_count_hour_us.reindex(f_full_timeline, fill_value=0)

    plt.figure(figsize=(12, 6))
    f_count_hour_eu.plot(kind='bar', color='skyblue', position=0, width=0.4, label='EU')
    f_count_hour_us.plot(kind='bar', color='orange', position=1, width=0.4, label='US')
    plt.xlabel('Hour')
    plt.ylabel('Number of flights')
    plt.title('Number of flights per Hour (EU vs US)')
    plt.xticks(rotation=90)
    plt.legend()

    # Make every tick that is at 00 time bold
    new_ticks = []
    for tick in plt.gca().get_xticklabels():
        if '00' in tick.get_text():
            tick.set_fontweight('bold')
        new_ticks.append(tick)
    plt.gca().set_xticklabels(new_ticks)
    plt.tight_layout()
    plt.show()


def main():
    #load_csv()
    load_json()
    stats_overall_departuredate()
    #stats_overall_departuredate_us_eu()

if __name__ == "__main__":
    main()