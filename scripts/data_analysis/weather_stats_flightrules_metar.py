import json, os
from datetime import datetime
import matplotlib.pyplot as plt
import pandas as pd

# Adjust as needed
#metar_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/metar'
#taf_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/taf'

metar_dir = '/home/sebastian/Desktop/thesis/weather_clean_2024_10_11/metar/'
taf_dir = '/home/sebastian/Desktop/thesis/weather_clean_2024_10_11/taf/'

################### METAR DEFINITIONS
metar_dates = []
metar_errors = 0

metar_files = [filename for filename in os.listdir(metar_dir)]

for file_name in metar_files:
    file_path = os.path.join(metar_dir, file_name)
    with open(file_path) as json_file:
        data = json.load(json_file)
        for o in data:
            try:
                # metar shows the weather as it is now, hence DateIssues is used
                metar_dates.append((datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ'), o['FlightRules']))
            except KeyError:
                metar_errors += 1
                continue

print(f'metar errors: {metar_errors}')

metar_df = pd.DataFrame(metar_dates, columns=['DateIssued', 'FlightRules'])

# Filter out entries before 2024-10-11 00:00:00 and after 2024-10-11 22:59:59
metar_start_date = datetime(2024, 10, 11, 0, 0, 0)
metar_end_date = datetime(2024, 10, 11, 21, 59, 59)
metar_df = metar_df[(metar_df['DateIssued'] >= metar_start_date) & (metar_df['DateIssued'] <= metar_end_date)]

# Create hour buckets and count the number of METAR reports per hour
metar_df['DateHour'] = metar_df['DateIssued'].dt.strftime('%Y-%m-%d %H')

# Count the number of occurrences of each FlightRules per hour
metar_flight_rules_counts = metar_df.groupby(['DateHour', 'FlightRules']).size().unstack(fill_value=0)

# Calculate the percentage of each FlightRules per hour
metar_flight_rules_percentages = metar_flight_rules_counts.div(metar_flight_rules_counts.sum(axis=1), axis=0) * 100

# Print the percentages of each flight rule per hour
print(metar_flight_rules_percentages)


# Plot the flight rules count per hour
plt.figure(figsize=(12, 6))
metar_flight_rules_counts.plot(kind='bar', stacked=True)
plt.title('Flight Rules Count per Hour on 2024-10-11')
plt.xlabel('Hour')
plt.ylabel('Count')
plt.xticks(rotation=45)
plt.legend(title='Flight Rules')
plt.tight_layout()
plt.show()