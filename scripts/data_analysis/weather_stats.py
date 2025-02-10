import json, os
from datetime import datetime, timedelta
import matplotlib.pyplot as plt
import pandas as pd
import seaborn as sns
# import matplotlib.dates as mdates
# import pytz

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
                metar_dates.append(datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ'))
            except KeyError:
                metar_errors += 1
                continue

print(f'metar errors: {metar_errors}')

metar_df = pd.DataFrame(metar_dates, columns=['DateIssued'])

# Filter out entries before 2024-10-11 00:00:00 and after 2024-10-11 22:59:59
metar_start_date = datetime(2024, 10, 11, 0, 0, 0)
metar_end_date = datetime(2024, 10, 11, 22, 59, 59)
metar_df = metar_df[(metar_df['DateIssued'] >= metar_start_date) & (metar_df['DateIssued'] <= metar_end_date)]

# Create hour buckets and count the number of METAR reports per hour
metar_df['DateHour'] = metar_df['DateIssued'].dt.strftime('%Y-%m-%d %H')
metar_hourly_counts = metar_df['DateHour'].value_counts().sort_index()

# Create a full timeline from min DateIssued to max DateIssued
# min_date = metar_df['DateIssued'].min().floor('h')
# max_date = metar_df['DateIssued'].max().ceil('h')
metar_full_timeline = pd.date_range(start=metar_start_date, end=metar_end_date, freq='h').strftime('%Y-%m-%d %H')

# Reindex the hourly counts to include all hours in the timeline
metar_hourly_counts = metar_hourly_counts.reindex(metar_full_timeline, fill_value=0)




################### TAF DEFINITIONS
taf_dates = []
taf_errors = 0

taf_files = [filename for filename in os.listdir(taf_dir)]

for file_name in taf_files:
    file_path = os.path.join(taf_dir, file_name)
    with open(file_path) as json_file:
        data = json.load(json_file)
        for o in data:
            try:
                #taf_dates.append((datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ'), datetime.strptime(o['Period']['DateStart'], '%Y-%m-%dT%H:%M:%SZ')))
                taf_dates.append((datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ'), datetime.strptime(o['Period']['DateStart'], '%Y-%m-%dT%H:%M:%SZ'), datetime.strptime(o['Period']['DateEnd'], '%Y-%m-%dT%H:%M:%SZ'), o['Ident']))
                # taf_dates.append((datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ'), datetime.strptime(o['Period']['DateEnd'], '%Y-%m-%dT%H:%M:%SZ'), o['Ident'], o['DateIssued']))
            except KeyError:
                taf_errors += 1
                continue

print(f'taf errors: {taf_errors}')

taf_df = pd.DataFrame(taf_dates, columns=['DateIssued', 'DateStart', 'DateEnd', 'Ident'])
# taf_df = pd.DataFrame(taf_dates, columns=['DateIssued', 'DateEnd', 'Ident', 'DateString'])

# Create hour buckets and count the number of TAF reports per hour
taf_df['DateHourStart'] = taf_df['DateStart'].dt.strftime('%Y-%m-%d %H')
taf_df['DateHourIssued'] = taf_df['DateIssued'].dt.strftime('%Y-%m-%d %H')

# Filter out entries before 2024-10-11 00:00:00 and after 2024-10-11 22:59:59
taf_start_date = datetime(2024, 10, 10, 22, 0, 0)
taf_end_date = datetime(2024, 10, 11, 20, 59, 59)
taf_df_filtered = taf_df[(taf_df['DateIssued'] >= taf_start_date) & (taf_df['DateIssued'] <= taf_end_date)]




def metar_stats():
    print(f'Maximum METAR reports per hour: {metar_hourly_counts.max()}')
    print(f'Minimum METAR reports per hour: {metar_hourly_counts.min()}')
    print(f'Mean METAR reports per hour: {metar_hourly_counts.mean()}')
    print(f'Median METAR reports per hour: {metar_hourly_counts.median()}')

    # Plot the hourly counts
    plt.figure(figsize=(12, 6))
    metar_hourly_counts.plot(kind='bar', color='skyblue')
    plt.xlabel('Hour')
    plt.ylabel('Number of METAR Reports')
    plt.title('Number of METAR Reports per Hour')
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()



def taf_stats_6h():
    ####################    Inspecting the surges that happen every 6 hours
    # taf_df_6h = taf_df_filtered[(taf_df_filtered['DateHourStart'].isin(['2024-10-10 00', '2024-10-11 06', '2024-10-11 12', '2024-10-11 18']))]
    taf_df_6h = taf_df_filtered[~taf_df_filtered['DateHourStart'].str.contains(' 00| 06| 12| 18')]
    taf_hourly_counts_6h = taf_df_6h['DateHourStart'].value_counts().sort_index()
    taf_df_6h['ForecastLength'] = (taf_df_6h['DateEnd'] - taf_df_6h['DateStart']).abs()
    taf_df_6h['PreLength'] = (taf_df_6h['DateStart'] - taf_df_6h['DateIssued']).abs()

    print()
    print(f'Maximum TAF reports in 6-hour intervals: {taf_hourly_counts_6h.max()}')
    print(f'Minimum TAF reports in 6-hour intervals: {taf_hourly_counts_6h.min()}')
    print(f'Mean TAF reports in 6-hour intervals: {taf_hourly_counts_6h.mean()}')
    print(f'Median TAF reports in 6-hour intervals: {taf_hourly_counts_6h.median()}')

    # Count the distinct Ident values in the 6-hour dataframe
    ident_counts_6h = taf_df_6h['Ident'].value_counts()

    # Print the top 10 Ident values
    # print()
    # print('Top 10 Ident values in 6-hour intervals:')
    # print(ident_counts_6h.head(10))

    # Print differences
    print()
    print(f'Maximum forecast length: {taf_df_6h['ForecastLength'].max()}')
    print(f'Minimum forecast length: {taf_df_6h['ForecastLength'].min()}')
    print(f'Mean forecast length: {taf_df_6h['ForecastLength'].mean()}')
    print(f'Median forecast length: {taf_df_6h['ForecastLength'].median()}')

    print()
    print(f'Maximum time forecast was done in advance: {taf_df_6h['PreLength'].max()}')
    print(f'Minimum time forecast was done in advance: {taf_df_6h['PreLength'].min()}')
    print(f'Mean time forecast was done in advance: {taf_df_6h['PreLength'].mean()}')
    print(f'Median time forecast was done in advance: {taf_df_6h['PreLength'].median()}')

    # Create buckets for the differences
    taf_df_6h['30MinBucket'] = (taf_df_6h['PreLength'] // timedelta(minutes=30)) * 30

    # Count the occurrences in each bucket
    taf_bucket_counts = taf_df_6h['30MinBucket'].value_counts().sort_index().reset_index()
    taf_bucket_counts.columns = ['30MinBucket', 'Count']

    # Plot the bucket counts
    plt.figure(figsize=(10, 6))
    plt.bar(taf_bucket_counts['30MinBucket'].astype(str), taf_bucket_counts['Count'], width=0.8, color='skyblue')
    plt.xlabel('30 Minute Buckets')
    plt.ylabel('Count')
    plt.title('TAF Difference between issue date and start date 30 Minute Buckets')
    #plt.gca().xaxis.set_major_locator(plt.MaxNLocator(nbins=len(taf_bucket_counts) ))
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()


def taf_stats_6h_inverse():
    ####################    Inspecting everything BUT those 6h surges
    taf_df_6h = taf_df_filtered[~taf_df_filtered['DateHourStart'].str.contains(' 00| 06| 12| 18')]
    taf_hourly_counts_6h = taf_df_6h['DateHourStart'].value_counts().sort_index()
    taf_df_6h['ForecastLength'] = (taf_df_6h['DateEnd'] - taf_df_6h['DateStart']).abs()
    taf_df_6h['PreLength'] = (taf_df_6h['DateStart'] - taf_df_6h['DateIssued']).abs()

    taf_min_date = taf_df_filtered['DateIssued'].min().floor('h')
    taf_max_date = taf_df_filtered['DateIssued'].max().ceil('h')

    # Plot the hourly counts
    plt.figure(figsize=(12, 6))
    taf_hourly_counts_6h.plot(kind='bar', color='skyblue')
    plt.xlabel('Hour')
    plt.ylabel('Number of TAF Reports')
    plt.title('Number of TAF Reports per Hour ')
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()

    print()
    print(f'Maximum TAF reports in 6-hour intervals: {taf_hourly_counts_6h.max()}')
    print(f'Minimum TAF reports in 6-hour intervals: {taf_hourly_counts_6h.min()}')
    print(f'Mean TAF reports in 6-hour intervals: {taf_hourly_counts_6h.mean()}')
    print(f'Median TAF reports in 6-hour intervals: {taf_hourly_counts_6h.median()}')

    # Print differences
    print()
    print(f'Maximum forecast length: {taf_df_6h['ForecastLength'].max()}')
    print(f'Minimum forecast length: {taf_df_6h['ForecastLength'].min()}')
    print(f'Mean forecast length: {taf_df_6h['ForecastLength'].mean()}')
    print(f'Median forecast length: {taf_df_6h['ForecastLength'].median()}')

    print()
    print(f'Maximum time forecast was done in advance: {taf_df_6h['PreLength'].max()}')
    print(f'Minimum time forecast was done in advance: {taf_df_6h['PreLength'].min()}')
    print(f'Mean time forecast was done in advance: {taf_df_6h['PreLength'].mean()}')
    print(f'Median time forecast was done in advance: {taf_df_6h['PreLength'].median()}')

    # Create buckets for the differences
    taf_df_6h['30MinBucket'] = (taf_df_6h['PreLength'] // timedelta(minutes=30)) * 30

    # Count the occurrences in each bucket
    taf_bucket_counts = taf_df_6h['30MinBucket'].value_counts().sort_index().reset_index()
    taf_bucket_counts.columns = ['30MinBucket', 'Count']

    # Plot the bucket counts
    plt.figure(figsize=(10, 6))
    plt.bar(taf_bucket_counts['30MinBucket'].astype(str), taf_bucket_counts['Count'], width=0.8, color='skyblue')
    plt.xlabel('30 Minute Buckets')
    plt.ylabel('Count')
    plt.title('TAF Difference between issue date and start date 30 Minute Buckets')
    #plt.gca().xaxis.set_major_locator(plt.MaxNLocator(nbins=len(taf_bucket_counts) ))
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()


def taf_stats_forecast_diff():
    ####################    Inspecting the time difference between DateIssued and DateStart/DateEnd
    # Calculate the difference between DateIssued and DateStart in taf_df
    #taf_df['Difference'] = (taf_df['DateStart'] - taf_df['DateIssued']).abs()
    taf_df['Difference'] = (taf_df['DateStart'] - taf_df['DateIssued']).abs()

    print()
    print(f'Maximum difference: {taf_df["Difference"].max()}')
    print(f'Minimum difference: {taf_df["Difference"].min()}')
    print(f'Mean difference: {taf_df["Difference"].mean()}')
    print(f'Median difference: {taf_df["Difference"].median()}')

    # Create buckets for the differences
    taf_df['5MinBucket'] = (taf_df['Difference'] // timedelta(minutes=5)) * 5
    taf_df['30MinBucket'] = (taf_df['Difference'] // timedelta(minutes=30)) * 30

    # Count the occurrences in each bucket
    #bucket_counts = taf_df['5MinBucket'].value_counts().sort_index().reset_index()
    #bucket_counts.columns = ['5MinBucket', 'Count']
    taf_bucket_counts = taf_df['30MinBucket'].value_counts().sort_index().reset_index()
    taf_bucket_counts.columns = ['30MinBucket', 'Count']

    # Plot the bucket counts
    plt.figure(figsize=(10, 6))
    #plt.bar(bucket_counts['5MinBucket'].astype(str), bucket_counts['Count'], width=0.8, color='skyblue')
    plt.bar(taf_bucket_counts['30MinBucket'].astype(str), taf_bucket_counts['Count'], width=0.8, color='skyblue')
    #plt.xlabel('5 Minute Buckets')
    plt.xlabel('30 Minute Buckets')
    plt.ylabel('Count')
    #plt.title('TAF DateIssued to DateStart Differences in 5 Minute Buckets')
    plt.title('TAF DateIssued to DateStart Differences in 30 Minute Buckets')
    plt.gca().xaxis.set_major_locator(plt.MaxNLocator(nbins=len(taf_bucket_counts) // 2))
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()


    # Plot a boxplot of the differences
    plt.figure(figsize=(10, 6))
    plt.boxplot(taf_df['Difference'].dt.total_seconds() / 60, vert=False, meanline=True, showmeans=True)
    plt.xlabel('Difference (minutes)')
    plt.title('Boxplot of TAF DateIssued to DateStart Differences')

    # Calculate quartiles, median, and mean
    q1 = taf_df['Difference'].quantile(0.25)
    q2 = taf_df['Difference'].quantile(0.5)
    q3 = taf_df['Difference'].quantile(0.75)
    median = taf_df['Difference'].median()
    mean = taf_df['Difference'].mean()

    # Calculate min and max without outliers
    min_no_outliers = taf_df['Difference'][taf_df['Difference'] >= q1 - 1.5 * (q3 - q1)].min()
    max_no_outliers = taf_df['Difference'][taf_df['Difference'] <= q3 + 1.5 * (q3 - q1)].max()

    # Add markers for quartiles, median, mean, min and max without outliers
    plt.axvline(x=min_no_outliers.total_seconds() / 60, ymin=0.5, color='purple', label=f'Min (no outliers): {min_no_outliers.total_seconds() / 3600:.2f} hours', linewidth=0.5, alpha=0)
    plt.axvline(x=q1.total_seconds() / 60, ymin=0.5, color='blue', label=f'Q1: {q1.total_seconds() / 3600:.2f} hours', linewidth=0.5, alpha=0)
    plt.axvline(x=median.total_seconds() / 60, ymin=0.5, color='y', label=f'Median: {median.total_seconds() / 3600:.2f} hours', linewidth=0.5, alpha=0)
    plt.axvline(x=mean.total_seconds() / 60, ymin=0.5, color='g', label=f'Mean: {mean.total_seconds() / 3600:.2f} hours', linewidth=0.5, alpha=0)
    plt.axvline(x=q3.total_seconds() / 60, ymin=0.5, color='orange', label=f'Q3: {q3.total_seconds() / 3600:.2f} hours', linewidth=0.5, alpha=0)
    plt.axvline(x=max_no_outliers.total_seconds() / 60, ymin=0.5, color='brown', label=f'Max (no outliers): {max_no_outliers.total_seconds() / 3600:.2f} hours', linewidth=0.5, alpha=0)

    plt.legend()
    plt.tight_layout()
    plt.show()


def taf_stats_above_24h():
    ############# Overall stats but with forecasts with a length less than 24 hours filtered out
    taf_df['Difference'] = (taf_df['DateEnd'] - taf_df['DateStart']).abs()
    taf_df_filtered = taf_df[(taf_df['DateIssued'] >= taf_start_date) & (taf_df['DateIssued'] <= taf_end_date) & (taf_df['Difference'] >= timedelta(hours=24))]

    taf_hourly_counts = taf_df_filtered['DateHourStart'].value_counts().sort_index()

    taf_min_date = taf_df_filtered['DateIssued'].min().floor('h')
    taf_max_date = taf_df_filtered['DateIssued'].max().ceil('h')
    taf_full_timeline = pd.date_range(start=taf_min_date, end=taf_max_date, freq='h').strftime('%Y-%m-%d %H')

    # Reindex the hourly counts to include all hours in the timeline
    taf_hourly_counts = taf_hourly_counts.reindex(taf_full_timeline, fill_value=0)

    print()
    print(f'Maximum TAF reports per hour: {taf_hourly_counts.max()}')
    print(f'Minimum TAF reports per hour: {taf_hourly_counts.min()}')
    print(f'Mean TAF reports per hour: {taf_hourly_counts.mean()}')
    print(f'Median TAF reports per hour: {taf_hourly_counts.median()}')

    # Plot the hourly counts
    plt.figure(figsize=(12, 6))
    taf_hourly_counts.plot(kind='bar', color='skyblue')
    plt.xlabel('Hour')
    plt.ylabel('Number of TAF Reports')
    plt.title('Number of TAF Reports per Hour (head and tail removed, anything with less than 24h forecast length filtered out)')
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()


def taf_stats_below_24h():
    ############# Overall stats but with forecasts with a length more than or equal 24 hours filtered out
    taf_df['Difference'] = (taf_df['DateEnd'] - taf_df['DateStart']).abs()
    taf_df_filtered = taf_df[(taf_df['DateIssued'] >= taf_start_date) & (taf_df['DateIssued'] <= taf_end_date) & (taf_df['Difference'] < timedelta(hours=24))]

    taf_hourly_counts = taf_df_filtered['DateHourStart'].value_counts().sort_index()

    taf_min_date = taf_df_filtered['DateIssued'].min().floor('h')
    taf_max_date = taf_df_filtered['DateIssued'].max().ceil('h')
    taf_full_timeline = pd.date_range(start=taf_min_date, end=taf_max_date, freq='h').strftime('%Y-%m-%d %H')

    # Reindex the hourly counts to include all hours in the timeline
    taf_hourly_counts = taf_hourly_counts.reindex(taf_full_timeline, fill_value=0)

    print()
    print(f'Maximum TAF reports per hour: {taf_hourly_counts.max()}')
    print(f'Minimum TAF reports per hour: {taf_hourly_counts.min()}')
    print(f'Mean TAF reports per hour: {taf_hourly_counts.mean()}')
    print(f'Median TAF reports per hour: {taf_hourly_counts.median()}')

    # Plot the hourly counts
    plt.figure(figsize=(12, 6))
    taf_hourly_counts.plot(kind='bar', color='skyblue')
    plt.xlabel('Hour')
    plt.ylabel('Number of TAF Reports')
    plt.title('Number of TAF Reports per Hour (head and tail removed, anything with 24h forecast length or more filtered out)')
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()


def taf_stats_overall():
    ####################    Inspecting hourly reports overall
    # Create a full timeline from min DateIssued to max DateIssued
    taf_hourly_counts = taf_df['DateHourIssued'].value_counts().sort_index()

    taf_min_date = taf_df_filtered['DateIssued'].min().floor('h')
    taf_max_date = taf_df_filtered['DateIssued'].max().ceil('h')
    taf_full_timeline = pd.date_range(start=taf_min_date, end=taf_max_date, freq='h').strftime('%Y-%m-%d %H')

    # Reindex the hourly counts to include all hours in the timeline
    taf_hourly_counts = taf_hourly_counts.reindex(taf_full_timeline, fill_value=0)

    print()
    print(f'Maximum TAF reports per hour: {taf_hourly_counts.max()}')
    print(f'Minimum TAF reports per hour: {taf_hourly_counts.min()}')
    print(f'Mean TAF reports per hour: {taf_hourly_counts.mean()}')
    print(f'Median TAF reports per hour: {taf_hourly_counts.median()}')

    # Plot the hourly counts
    plt.figure(figsize=(12, 6))
    taf_hourly_counts.plot(kind='bar', color='skyblue')
    plt.xlabel('Hour')
    plt.ylabel('Number of TAF Reports')
    plt.title('Number of TAF Reports per Hour (head and tail removed)')
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()


def main():
    # taf_stats_overall()
    # taf_stats_above_24h()
    # taf_stats_below_24h()
    # taf_stats_6h()
    taf_stats_6h_inverse()
    # taf_stats_forecast_diff()
    # metar_stats()
    # print("The end")

if __name__ == "__main__":
    main()