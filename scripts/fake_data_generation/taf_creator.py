import json
import uuid
import random
from datetime import datetime, timedelta
import numpy as np
from scipy.stats import truncnorm

# SETTINGS
file_start_date = datetime(2025, 1, 1)
file_end_date = datetime(2025, 1, 2)

minimum_forecast_hours = 1
maximum_forecast_hours = 24

hours_spacing = 4

flightrule_list = ["VFR"]
changes_list = ["BECOMING", "TEMPORARY"]

with open('/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/europe_airports.json', 'r') as f:
    airports_data = json.load(f)
airports = [airport['ICAO'] for airport in airports_data]


def create_taf_conditions(date_start, date_end):
    conditions = []
    # Get number of conditions (based on weights from data analysis)

    # Create end of first condition, which is minimum 1 hour after start, maximum "date_end - num_of_conditions*1h"

    # Create the end of the first condition, minimum 1 hour after start, maximum the amount of hours left of the forecast
    random_hours_after_start = random.randint(1, int((date_end - date_start).total_seconds() // 3600) )
    initial_condition_end = date_start + timedelta(hours=random_hours_after_start)

    conditions.append({
        "FlightRules": random.choice(flightrule_list),
        "Period": {
            "DateStart": date_start.strftime("%Y-%m-%dT%H:%M:%SZ"),
            "DateEnd": initial_condition_end.strftime("%Y-%m-%dT%H:%M:%SZ")
        }
    })

    current_end = initial_condition_end

    while current_end < date_end:
        current_start = current_end

        # Create the end of the condition, minimum 1 hour after start, maximum the amount of hours left of the forecast
        random_hours_after_start = random.randint(1, int((date_end - current_start).total_seconds() // 3600) )
        current_end = current_start + timedelta(hours=random_hours_after_start)
        conditions.append({
            "FlightRules": random.choice(flightrule_list),
            "Period": {
                "DateStart": current_start.strftime("%Y-%m-%dT%H:%M:%SZ"),
                "DateEnd": current_end.strftime("%Y-%m-%dT%H:%M:%SZ")
            },
            "Change": random.choice(changes_list)
        })
    return conditions


def get_taf_for_airport(icao):
    current_date = file_start_date + timedelta(hours=random.randint(0, hours_spacing))
    taf_objects = []

    while current_date < file_end_date:
        random_hours = random.randint(minimum_forecast_hours, maximum_forecast_hours - 1)
        start = current_date + timedelta(hours=random_hours)
        end = current_date + timedelta(hours=random.randint(random_hours + 1, maximum_forecast_hours))

        taf_objects.append({
            "DateIssued": current_date.strftime("%Y-%m-%dT%H:%M:%SZ"),
            "ID": str(uuid.uuid4()),
            "Ident": icao,
            "Period": {
                "DateStart": start.strftime("%Y-%m-%dT%H:%M:%SZ"),
                "DateEnd": end.strftime("%Y-%m-%dT%H:%M:%SZ")
            },
            "Conditions": create_taf_conditions(start, end),
            "Text": ""
        })
        current_date = current_date + timedelta(hours=hours_spacing)
    
    return taf_objects


def create_base_layer():
    min_prhr, max_prhr, mean_prhr, median_prhr, std_prhr = 210, 4025, 1048, 698, 1139
    min_forecast, max_forecast, mean_forecast, median_forecast, std_forecast = 0, 510, 44, 52, 33 # minutes
    min_length, max_length, mean_length, median_length, std_length = 1, 33, 15.5, 12, 8 # hours

    for hour in range(0, 24):
        #for i in range(10):  # Assuming you want to generate 10 random values per hour
            print(round(get_random_value(mean_prhr, median_prhr, min_prhr, max_prhr, std_prhr)))


def get_random_value(mean, median, min, max, std):
    a,b = (min - mean) / std, (max - mean) / std
    return truncnorm.rvs(a, b, loc=mean, scale=std)


def main():
    create_base_layer()


if __name__ == "__main__":
    main()