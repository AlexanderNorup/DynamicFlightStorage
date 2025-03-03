import json, os
from datetime import datetime, timedelta
import matplotlib.pyplot as plt
import pandas as pd
import matplotlib.dates as mdates
import pytz

flight_df = pd.DataFrame()

def load_json():
    global flight_df
    flight_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/flights/'

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
                continue

    flight_df = pd.DataFrame(flight_data)
    flight_df['Hour'] = flight_df['ScheduledTimeOfDeparture'].dt.strftime('%Y-%m-%d %H')
    flight_df['Day'] = flight_df['ScheduledTimeOfDeparture'].dt.strftime('%Y-%m-%d')
    flight_df['HourOnly'] = flight_df['ScheduledTimeOfDeparture'].dt.strftime('%H')


def load_csv():
    global flight_df
    flight_dir = '/home/sebastian/Desktop/thesis/Real_flights.csv'
    # Read from the CSV file
    flight_df = pd.read_csv(flight_dir, parse_dates=['Takeoff_Time'])
    flight_df['Hour'] = flight_df['Takeoff_Time'].dt.strftime('%Y-%m-%d %H')
    flight_df['Day'] = flight_df['Takeoff_Time'].dt.strftime('%Y-%m-%d')
    flight_df['HourOnly'] = flight_df['Takeoff_Time'].dt.strftime('%H')
    print(flight_df.head())
    print(f"Flight data departure ranges from {flight_df['Hour'].min()} to {flight_df['Hour'].max()}")


    # Filtering to only be concerned with 23-05-10 until 23-06-07
    start_date = '2023-05-10'
    end_date = '2023-06-07'
    mask = (flight_df['Takeoff_Time'] >= start_date) & (flight_df['Takeoff_Time'] <= end_date)
    flight_df = flight_df.loc[mask]
    # Filter out flights on the days 2023-05-23 and 2023-05-24 (anomalies in data)
    exclude_dates = ['2023-05-23', '2023-05-24']
    flight_df = flight_df[~flight_df['Day'].isin(exclude_dates)]


def stats_overall_departuredate():
    global flight_df
    f_count_hour = flight_df['HourOnly'].value_counts().sort_index()
    f_count_day = flight_df['Day'].value_counts().sort_index()
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

    f_count_hour.plot(kind='bar', color='skyblue', figsize=(12, 6))
    plt.xlabel('Hour')
    plt.ylabel('Number of flights')
    plt.xticks(rotation=90)

    plt.tight_layout()
    plt.savefig('/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/data_analysis/000000_flight.pdf')
    plt.show()


def main():
    #load_csv()
    load_json()
    stats_overall_departuredate()

if __name__ == "__main__":
    main()