import json, os
from datetime import datetime
import matplotlib.pyplot as plt
import pandas as pd
import pytz
import matplotlib.dates as mdates

# Adjust as needed
metar_dir = '/home/sebastian/Desktop/thesis/weather_clean_2024_10_11/metar/'
taf_dir = '/home/sebastian/Desktop/thesis/weather_clean_2024_10_11/taf/'
flight_dir = '/home/sebastian/Desktop/thesis/2024_10_10_flights/'

weather_dates = []
flight_dates = []
metar_errors = 0
taf_errors = 0
flight_errors = 0

# Get the date start for each taf and metar
# metar
metar_files = [filename for filename in os.listdir(metar_dir)]

for file_name in metar_files:
    file_path = os.path.join(metar_dir, file_name)
    with open(file_path) as json_file:
        data = json.load(json_file)
        for o in data:
            try:
                # metar shows the weather as it is now, hence DateIssues is used
                weather_dates.append(datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ'))
            except KeyError:
                metar_errors += 1
                continue


# taf
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


# Get date departure for each flight
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

print(f'taf errors: {taf_errors}, metar errors: {metar_errors}, flight errors: {flight_errors}')

# Create hour buckets and combine into one dataframe
weather_df = pd.DataFrame(weather_dates, columns=['Dates'])
weather_df['DateHour'] = weather_df['Dates'].dt.strftime('%Y-%m-%d %H')
w_counts = weather_df['DateHour'].value_counts().sort_index()


flight_df = pd.DataFrame(flight_dates, columns=['Dates'])
flight_df['DateHour'] = flight_df['Dates'].dt.strftime('%Y-%m-%d %H')
f_count = flight_df['DateHour'].value_counts().sort_index()


weather_counts = weather_df.groupby('DateHour').size().reset_index(name='weather_count')
flight_counts = flight_df.groupby('DateHour').size().reset_index(name='flight_count')

combined_counts = pd.merge(weather_counts, flight_counts, on='DateHour', how='outer')
combined_counts['DateHour'] = pd.to_datetime(combined_counts['DateHour'])


# full datetime index that covers both datasets and fills in any gaps
start_time = combined_counts['DateHour'].min()
end_time = combined_counts['DateHour'].max()
full_time_index = pd.date_range(start=start_time, end=end_time, freq='h')

# reindex to full time index
combined_counts_resampled = combined_counts.set_index('DateHour').reindex(full_time_index)

'''

# plotting 
fig, ax1 = plt.subplots(figsize=(12, 6))

# weather data (skyblue)
ax1.bar(combined_counts_resampled.index, combined_counts_resampled['weather_count'], 
        color='skyblue', label='Weather', width=0.1)
ax1.set_xlabel('Date and Hour')
ax1.set_ylabel('Weather Count', color='skyblue')
ax1.tick_params(axis='y', labelcolor='skyblue')

# flight data (orange)
ax2 = ax1.twinx()
ax2.bar(combined_counts_resampled.index, combined_counts_resampled['flight_count'], 
        color='orange', alpha=0.5, label='Flights', width=0.1)
ax2.set_ylabel('Flight Count', color='orange')
ax2.tick_params(axis='y', labelcolor='orange')

# readability
ax1.xaxis.set_major_formatter(mdates.DateFormatter('%m-%d'))
ax1.xaxis.set_major_locator(mdates.DayLocator(interval=2))

ax1.set_xlim([start_time, end_time])
ax1.set_ylim([combined_counts_resampled['weather_count'].min(), combined_counts_resampled['weather_count'].max()])
ax2.set_ylim([combined_counts_resampled['flight_count'].min(), combined_counts_resampled['flight_count'].max()])
plt.setp(ax1.xaxis.get_majorticklabels(), rotation=70)

plt.tight_layout()
plt.show()
'''