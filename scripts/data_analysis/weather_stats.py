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


def metar_stats():
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
    start_date = datetime(2024, 10, 11, 0, 0, 0)
    end_date = datetime(2024, 10, 11, 22, 59, 59)
    metar_df = metar_df[(metar_df['DateIssued'] >= start_date) & (metar_df['DateIssued'] <= end_date)]

    # Create hour buckets and count the number of METAR reports per hour
    metar_df['DateHour'] = metar_df['DateIssued'].dt.strftime('%Y-%m-%d %H')
    hourly_counts = metar_df['DateHour'].value_counts().sort_index()

    # Create a full timeline from min DateIssued to max DateIssued
    min_date = metar_df['DateIssued'].min().floor('h')
    max_date = metar_df['DateIssued'].max().ceil('h')
    full_timeline = pd.date_range(start=start_date, end=end_date, freq='h').strftime('%Y-%m-%d %H')

    # Reindex the hourly counts to include all hours in the timeline
    hourly_counts = hourly_counts.reindex(full_timeline, fill_value=0)

    print(f'Maximum METAR reports per hour: {hourly_counts.max()}')
    print(f'Minimum METAR reports per hour: {hourly_counts.min()}')
    print(f'Mean METAR reports per hour: {hourly_counts.mean()}')
    print(f'Median METAR reports per hour: {hourly_counts.median()}')

    # Plot the hourly counts
    plt.figure(figsize=(12, 6))
    hourly_counts.plot(kind='bar', color='skyblue')
    plt.xlabel('Hour')
    plt.ylabel('Number of METAR Reports')
    plt.title('Number of METAR Reports per Hour')
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()




def taf_stats():
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
                    taf_dates.append((datetime.strptime(o['DateIssued'], '%Y-%m-%dT%H:%M:%SZ'), datetime.strptime(o['Period']['DateEnd'], '%Y-%m-%dT%H:%M:%SZ'), o['Ident']))
                except KeyError:
                    taf_errors += 1
                    continue

    print(f'taf errors: {taf_errors}')

    #taf_df = pd.DataFrame(taf_dates, columns=['DateIssued', 'DateStart'])
    taf_df = pd.DataFrame(taf_dates, columns=['DateIssued', 'DateEnd', 'Ident'])

    # Create hour buckets and count the number of TAF reports per hour
    taf_df['DateHour'] = taf_df['DateIssued'].dt.strftime('%Y-%m-%d %H')

    # Filter out entries before 2024-10-11 00:00:00 and after 2024-10-11 22:59:59
    start_date = datetime(2024, 10, 10, 22, 0, 0)
    end_date = datetime(2024, 10, 11, 20, 59, 59)
    taf_df_filtered = taf_df[(taf_df['DateIssued'] >= start_date) & (taf_df['DateIssued'] <= end_date)]


    ####################    Inspecting the surges that happen every 6 hours
    taf_df_6h = taf_df_filtered[(taf_df_filtered['DateHour'].isin(['2024-10-10 23', '2024-10-11 05', '2024-10-11 11', '2024-10-11 17']))]
    hourly_counts_6h = taf_df_6h['DateHour'].value_counts().sort_index()

    print()
    print(f'Maximum TAF reports in 6-hour intervals: {hourly_counts_6h.max()}')
    print(f'Minimum TAF reports in 6-hour intervals: {hourly_counts_6h.min()}')
    print(f'Mean TAF reports in 6-hour intervals: {hourly_counts_6h.mean()}')
    print(f'Median TAF reports in 6-hour intervals: {hourly_counts_6h.median()}')

    # Count the distinct Ident values in the 6-hour dataframe
    ident_counts_6h = taf_df_6h['Ident'].value_counts()

    # Print the top 10 Ident values
    print()
    print('Top 10 Ident values in 6-hour intervals:')
    print(ident_counts_6h.head(10))
    

    ####################    Inspecting hourly reports overall
    # Create a full timeline from min DateIssued to max DateIssued
    hourly_counts = taf_df['DateHour'].value_counts().sort_index()

    min_date = taf_df_filtered['DateIssued'].min().floor('h')
    max_date = taf_df_filtered['DateIssued'].max().ceil('h')
    full_timeline = pd.date_range(start=min_date, end=max_date, freq='h').strftime('%Y-%m-%d %H')

    # Reindex the hourly counts to include all hours in the timeline
    hourly_counts = hourly_counts.reindex(full_timeline, fill_value=0)

    print()
    print(f'Maximum TAF reports per hour: {hourly_counts.max()}')
    print(f'Minimum TAF reports per hour: {hourly_counts.min()}')
    print(f'Mean TAF reports per hour: {hourly_counts.mean()}')
    print(f'Median TAF reports per hour: {hourly_counts.median()}')

    # Plot the hourly counts
    plt.figure(figsize=(12, 6))
    hourly_counts.plot(kind='bar', color='skyblue')
    plt.xlabel('Hour')
    plt.ylabel('Number of TAF Reports')
    plt.title('Number of TAF Reports per Hour (head and tail removed)')
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()


    ####################    Inspecting the time difference between DateIssued and DateStart/DateEnd
    # Calculate the difference between DateIssued and DateStart in taf_df
    #taf_df['Difference'] = (taf_df['DateStart'] - taf_df['DateIssued']).abs()
    taf_df['Difference'] = (taf_df['DateEnd'] - taf_df['DateIssued']).abs()

    print()
    print(f'Maximum difference: {taf_df["Difference"].max()}')
    print(f'Minimum difference: {taf_df["Difference"].min()}')
    print(f'Mean difference: {taf_df["Difference"].mean()}')
    print(f'Median difference: {taf_df["Difference"].median()}')

    # Create 5 minute buckets for the differences
    taf_df['5MinBucket'] = (taf_df['Difference'] // timedelta(minutes=5)) * 5
    taf_df['30MinBucket'] = (taf_df['Difference'] // timedelta(minutes=30)) * 30

    # Count the occurrences in each bucket
    #bucket_counts = taf_df['5MinBucket'].value_counts().sort_index().reset_index()
    #bucket_counts.columns = ['5MinBucket', 'Count']
    bucket_counts = taf_df['30MinBucket'].value_counts().sort_index().reset_index()
    bucket_counts.columns = ['30MinBucket', 'Count']

    # Plot the bucket counts
    plt.figure(figsize=(10, 6))
    #plt.bar(bucket_counts['5MinBucket'].astype(str), bucket_counts['Count'], width=0.8, color='skyblue')
    plt.bar(bucket_counts['30MinBucket'].astype(str), bucket_counts['Count'], width=0.8, color='skyblue')
    #plt.xlabel('5 Minute Buckets')
    plt.xlabel('30 Minute Buckets')
    plt.ylabel('Count')
    #plt.title('TAF DateIssued to DateStart Differences in 5 Minute Buckets')
    plt.title('TAF DateIssued to DateEnd Differences in 30 Minute Buckets')
    plt.gca().xaxis.set_major_locator(plt.MaxNLocator(nbins=len(bucket_counts) // 2))
    plt.xticks(rotation=90)
    plt.tight_layout()
    plt.show()


def main():
    taf_stats()
    #metar_stats()

if __name__ == "__main__":
    main()