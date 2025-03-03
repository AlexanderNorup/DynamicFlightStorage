import json, os
from datetime import datetime
import matplotlib.pyplot as plt
import pandas as pd

# Adjust as needed
#taf_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/taf'
taf_dir = '/home/sebastian/Desktop/thesis/weather_clean_2024_10_11/taf/'

################### TAF DEFINITIONS
taf_dates = []
taf_codes = []
taf_errors = 0

taf_files = [filename for filename in os.listdir(taf_dir)]

for file_name in taf_files:
    file_path = os.path.join(taf_dir, file_name)
    with open(file_path) as json_file:
        data = json.load(json_file)
        for o in data:
            try:
                taf_dates.append((datetime.strptime(o['Period']['DateStart'], '%Y-%m-%dT%H:%M:%SZ'), datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ'), len(o['Conditions'])))
                for cond in o['Conditions']:
                    taf_codes.append((cond['FlightRules'], datetime.strptime(o['Period']['DateStart'], '%Y-%m-%dT%H:%M:%SZ'), datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ')))
            except KeyError:
                taf_errors += 1
                continue

print(f'taf errors: {taf_errors}')

taf_df = pd.DataFrame(taf_dates, columns=['DateStart', 'DateIssued', 'Conditions'])

# Create hour buckets and count the number of TAF reports per hour
taf_df['DateHourStart'] = taf_df['DateStart'].dt.strftime('%Y-%m-%d %H')

# Filter out entries before 2024-10-10 20:00:00 and after 2024-10-11 20:59:59, both for issue date and start date
taf_start_date = datetime(2024, 10, 10, 20, 0, 0)
taf_end_date = datetime(2024, 10, 11, 20, 59, 59)
taf_temp = taf_df[(taf_df['DateIssued'] >= taf_start_date) & (taf_df['DateIssued'] <= taf_end_date)]
taf_df = taf_temp[(taf_df['DateStart'] >= taf_start_date) & (taf_df['DateStart'] <= taf_end_date)]

taf_stats = taf_df.groupby('DateHourStart')['Conditions'].agg(['mean', 'min', 'max', 'median', 'std']).reset_index()
print(taf_stats)

taf_conditions_percentage = taf_df['Conditions'].value_counts(normalize=True) * 100
print(taf_conditions_percentage)


# Analysing the flight rules
taf_codes_df = pd.DataFrame(taf_codes, columns=['Rule', 'DateStart', 'DateIssued'])
taf_codes_df['DateHourStart'] = taf_codes_df['DateStart'].dt.strftime('%Y-%m-%d %H')
taf_codes_df = taf_codes_df[(taf_codes_df['DateStart'] >= taf_start_date) & (taf_codes_df['DateStart'] <= taf_end_date)]

# Count the number of occurrences of each FlightRules per hour
taf_flight_rules_counts = taf_codes_df.groupby(['DateHourStart', 'Rule']).size().unstack(fill_value=0)
taf_flight_rules_percentages = taf_flight_rules_counts.div(taf_flight_rules_counts.sum(axis=1), axis=0) * 100
print(taf_flight_rules_percentages)

taf_flight_rules_stats = taf_flight_rules_percentages.agg(['mean', 'median', 'min', 'max', 'std']).transpose()
print(taf_flight_rules_stats)

# Filter the data to include only the hours 00, 06, 12, and 18
filtered_hours = ['00', '06', '12', '18']
taf_flight_rules_percentages_filtered = taf_flight_rules_percentages[taf_flight_rules_percentages.index.str[-2:].isin(filtered_hours)]
print(taf_flight_rules_percentages_filtered)
taf_flight_rules_stats_filtered = taf_flight_rules_percentages_filtered.agg(['mean', 'median', 'min', 'max', 'std']).transpose()
print(taf_flight_rules_stats_filtered)

flight_rules_colors = {
    'VFR': '#2ca02c',  # green
    'MVFR': '#1f77b4',  # blue
    'IFR': '#ff7f0e',  # orange
    'LIFR': '#d62728'  # red
}

taf_flight_rules_counts.plot(kind='bar', color=[flight_rules_colors.get(x, '#333333') for x in taf_flight_rules_counts.columns], stacked=True, figsize=(12, 6))
plt.xlabel('Hour')
plt.ylabel('Count')
plt.xticks(rotation=45)
plt.legend(title='Flight Rules')
plt.tight_layout()
plt.savefig('/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/data_analysis/000000_taf.pdf')
plt.show()