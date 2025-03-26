import json, os
from datetime import datetime
import pandas as pd

# Adjust as needed
taf_dir = '/home/sebastian/Desktop/thesis/weather_clean_2024_10_11/taf/'

################### TAF DEFINITIONS
taf_errors = 0
taf_conditions = []

taf_files = [filename for filename in os.listdir(taf_dir)]

for file_name in taf_files:
    file_path = os.path.join(taf_dir, file_name)
    with open(file_path) as json_file:
        data = json.load(json_file)
        for o in data:
            try:
                for cond in o['Conditions']:
                    taf_conditions.append((
                        file_name,
                        o['ID'],
                        o['Period']['DateStart'],
                        datetime.strptime(cond['Period']['DateStart'], '%Y-%m-%dT%H:%M:%SZ'), 
                        datetime.strptime(cond['Period']['DateEnd'], '%Y-%m-%dT%H:%M:%SZ')))
            except KeyError:
                taf_errors += 1
                continue

print(f'taf errors: {taf_errors}')

taf_conditions_df = pd.DataFrame(taf_conditions, columns=['FileName', 'ID', 'MainStart', 'Start', 'End'])
taf_conditions_df['DurationHours'] = (taf_conditions_df['End'] - taf_conditions_df['Start']).dt.total_seconds() / 3600
taf_conditions_df = taf_conditions_df[(taf_conditions_df['DurationHours'] >= 0) & (taf_conditions_df['DurationHours'] <= 48)]

# Calculate statistics
min_duration = taf_conditions_df['DurationHours'].min()
max_duration = taf_conditions_df['DurationHours'].max()
mean_duration = taf_conditions_df['DurationHours'].mean()
median_duration = taf_conditions_df['DurationHours'].median()
std_duration = taf_conditions_df['DurationHours'].std()

# Print statistics
print(f"Min Duration (hours): {min_duration}")
print(f"Max Duration (hours): {max_duration}")
print(f"Mean Duration (hours): {mean_duration}")
print(f"Median Duration (hours): {median_duration}")
print(f"Standard Deviation (hours): {std_duration}")

# Find and print the elements with min/max DurationHours
min_duration_row = taf_conditions_df.loc[taf_conditions_df['DurationHours'].idxmin()]
max_duration_row = taf_conditions_df.loc[taf_conditions_df['DurationHours'].idxmax()]

print("\nElement with Min Duration:")
print(min_duration_row)

print("\nElement with Max Duration:")
print(max_duration_row)