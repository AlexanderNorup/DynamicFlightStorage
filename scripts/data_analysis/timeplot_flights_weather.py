import json, os
from datetime import datetime, timedelta
import matplotlib.pyplot as plt
import pandas as pd
import matplotlib.dates as mdates
import pytz

# Adjust as needed
#metar_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/metar'
#taf_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/taf'

metar_dir = '/home/sebastian/Desktop/thesis/weather_clean_2024_10_11/metar/'
taf_dir = '/home/sebastian/Desktop/thesis/weather_clean_2024_10_11/taf/'


weather_dates = []
flight_dates = []
metar_errors = 0
taf_errors = 0
flight_errors = 0

metar_files = [filename for filename in os.listdir(metar_dir)]

for file_name in metar_files:
    file_path = os.path.join(metar_dir, file_name)
    with open(file_path) as json_file:
        data = json.load(json_file)
        for o in data:
            try:
                weather_dates.append(datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ'))
            except KeyError:
                metar_errors += 1
                continue


taf_files = [filename for filename in os.listdir(taf_dir)]

for file_name in taf_files:
    file_path = os.path.join(taf_dir, file_name)
    with open(file_path) as json_file:
        data = json.load(json_file)
        for o in data:
            try:
                # taf shows weather forecast, hence using datestart
                weather_dates.append(datetime.strptime(o['Period']['DateStart'], '%Y-%m-%dT%H:%M:%SZ'))
            except KeyError:
                taf_errors += 1
                continue

weather_df = pd.DataFrame(weather_dates, columns=['Dates'])
weather_df['Hour'] = weather_df['Dates'].dt.strftime('%Y-%m-%d %H')
weather_df['Day'] = weather_df['Dates'].dt.strftime('%Y-%m-%d')
weather_df['Month'] = weather_df['Dates'].dt.strftime('%Y-%m')

flight_df = pd.DataFrame()

def load_flights_json():
    global flight_df
    #flight_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/flights'
    flight_dir = '/home/sebastian/Desktop/thesis/2024_10_10_flights/'
    flight_files = [filename for filename in os.listdir(flight_dir)]

    for file_name in flight_files:
        file_path = os.path.join(flight_dir, file_name)
        with open(file_path) as json_file:
            data = json.load(json_file)
            try:
                flight_dates.append(
                    datetime.fromisoformat(data['ScheduledTimeOfDeparture'])
                    .astimezone(pytz.utc)
                )
            except KeyError:
                flight_errors += 1
                continue
    flight_df = pd.DataFrame(flight_dates, columns=['Dates'])
    flight_df['Hour'] = flight_df['Dates'].dt.strftime('%Y-%m-%d %H')
    flight_df['Day'] = flight_df['Dates'].dt.strftime('%Y-%m-%d')
    flight_df['Month'] = flight_df['Dates'].dt.strftime('%Y-%m')


def load_flights_csv():
    global flight_df
    flight_dir = '/home/sebastian/Desktop/thesis/Real_flights.csv'

    # Read from the CSV file
    flight_df = pd.read_csv(flight_dir, parse_dates=['Takeoff_Time'])
    flight_df['Hour'] = flight_df['Takeoff_Time'].dt.strftime('%Y-%m-%d %H')
    flight_df['Day'] = flight_df['Takeoff_Time'].dt.strftime('%Y-%m-%d')
    flight_df['Month'] = flight_df['Takeoff_Time'].dt.strftime('%Y-%m')

load_flights_csv()
bucket_size = 'Day' # 'Hour' 'Month'

print(f"Flight data ranges from {flight_df[bucket_size].min()} to {flight_df[bucket_size].max()}")
print(f"Weather data ranges from {weather_df[bucket_size].min()} to {weather_df[bucket_size].max()}")

flight_counts = flight_df.groupby(bucket_size).size().reset_index(name='flight_count')
weather_counts = weather_df.groupby(bucket_size).size().reset_index(name='weather_count')

combined_counts = pd.merge(weather_counts, flight_counts, on=bucket_size, how='outer')
combined_counts[bucket_size] = pd.to_datetime(combined_counts[bucket_size])


# full datetime index that covers both datasets and fills in any gaps
start_time = combined_counts[bucket_size].min()
end_time = combined_counts[bucket_size].max()
full_time_index = pd.date_range(start=start_time, end=end_time, freq='D')
# reindex to full time index
combined_counts_resampled = combined_counts.set_index(bucket_size).reindex(full_time_index)

fig, ax1 = plt.subplots(figsize=(12, 6))

ax1.bar(combined_counts_resampled.index, combined_counts_resampled['flight_count'], 
        color='green', alpha=0.8, label='Flight', width=timedelta(days=1))
# ax1.set_xlabel('Date and Hour')
ax1.set_xlabel('Year-Month')
ax1.set_ylabel('Flight Count', color='green')
ax1.tick_params(axis='y', labelcolor='green')

ax2 = ax1.twinx()
ax2.bar(combined_counts_resampled.index, combined_counts_resampled['weather_count'], 
        color='blue', alpha=0.8, label='Weather', width=timedelta(days=1))
ax2.set_ylabel('Weather Count', color='blue')
ax2.tick_params(axis='y', labelcolor='blue')

ax1.xaxis.set_major_formatter(mdates.DateFormatter('%Y-%m'))
ax1.xaxis.set_major_locator(mdates.DayLocator(interval=30))

# ax1.set_xlim([datetime(2024, 9, 23), datetime(2024, 10, 18)])
# ax1.set_xlim([start_time - timedelta(days=2), end_time + timedelta(days=2)])
ax1.set_ylim([0, combined_counts_resampled['flight_count'].max()])
ax2.set_ylim([0, combined_counts_resampled['weather_count'].max()])
plt.setp(ax1.xaxis.get_majorticklabels(), rotation=70)


plt.tight_layout()
# plt.savefig('/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/data_analysis/flight_weather_timeplot.pdf')
plt.show()