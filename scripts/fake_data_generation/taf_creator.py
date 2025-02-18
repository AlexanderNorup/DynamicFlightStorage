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

flightrule_list = ['IFR', 'LIFR', 'MVFR', 'VFR']
weights_6h = [0.10736553, 0.03444521, 0.20748245, 0.65070681]
weights_not_6h = [0.10449660, 0.06667038, 0.18332601, 0.64550701]

num_of_conditions_list = [1,2,3,4,5,6,7,8,9,10,11,12]
weights_condition_num = [0.17901357, 0.25357524, 0.25475820, 0.16936249, 0.07911782, 0.04177842, 0.01293058, 0.00555172, 0.00260017, 0.00069104, 0.00052706, 0.00009370]

changes_list = ["BECOMING", "TEMPORARY"]

with open('/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/europe_airports.json', 'r') as f:
    airports_data = json.load(f)
airports = [airport['ICAO'] for airport in airports_data]


def create_taf_conditions(date_start, date_end, base_layer=True): # dates must be in whole hours
    conditions = []
    # Get number of conditions (based on weights from data analysis)
    number_of_conditions= random.choices(num_of_conditions_list, weights=weights_condition_num)[0]

    taf_length = int((date_end - date_start).total_seconds() / (60*60))
    if taf_length < number_of_conditions: 
        number_of_conditions = taf_length


    # Create end of first condition, which is minimum 1 hour after start, maximum "date_end - num_of_conditions*1h"
    number_of_conditions -= 1
    random_hours_after_start = random.randint(1,  taf_length - number_of_conditions)
    initial_condition_end = date_start + timedelta(hours=random_hours_after_start)
    taf_length -= random_hours_after_start

    weights = weights_not_6h if base_layer else weights_6h

    conditions.append({
        "FlightRules": random.choices(flightrule_list, weights=weights)[0],
        "Period": {
            "DateStart": date_start.strftime("%Y-%m-%dT%H:%M:%SZ"),
            "DateEnd": initial_condition_end.strftime("%Y-%m-%dT%H:%M:%SZ")
        }
    })
    

    current_end = initial_condition_end

    while number_of_conditions > 0:
        current_start = current_end

        number_of_conditions -= 1
        if number_of_conditions == 0:
            condition_length = taf_length
        else:
            condition_length = random.randint(1,  taf_length - number_of_conditions)
        current_end = current_start + timedelta(hours=condition_length)
        taf_length -= condition_length

        conditions.append({
            "FlightRules": random.choices(flightrule_list, weights=weights)[0],
            "Period": {
                "DateStart": current_start.strftime("%Y-%m-%dT%H:%M:%SZ"),
                "DateEnd": current_end.strftime("%Y-%m-%dT%H:%M:%SZ")
            },
            "Change": random.choice(changes_list)
        })

    return conditions


def get_taf(icao, date_start, date_end, date_issued, base_layer=True):
        return {
        "DateIssued": date_issued.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "ID": str(uuid.uuid4()),
        "Ident": icao,
        "Period": {
            "DateStart": date_start.strftime("%Y-%m-%dT%H:%M:%SZ"),
            "DateEnd": date_end.strftime("%Y-%m-%dT%H:%M:%SZ")
        },
        "Conditions": create_taf_conditions(date_start, date_end, base_layer),
        "Text": ""
    }


def create_base_layer():
    min_prhr, max_prhr, mean_prhr, median_prhr, std_prhr = 210, 4025, 1048, 698, 1139
    min_forecast, max_forecast, mean_forecast, median_forecast, std_forecast = 0, 510, 44, 52, 33 # minutes
    min_length, max_length, mean_length, median_length, std_length = 1, 33, 15.5, 12, 8 # hours

    for hour in range(0, 24):
        tafs = []
        date_start = file_start_date + timedelta(hours=hour)
        for i in range(round(get_random_value(mean_prhr, median_prhr, min_prhr, max_prhr, std_prhr))):
            icao = random.choice(airports)
            date_issued = date_start - timedelta(minutes=round(get_random_value(mean_forecast, median_forecast, min_forecast, max_forecast, std_forecast)))
            date_end = date_start + timedelta(hours=round(get_random_value(mean_length, median_length, min_length, max_length, std_length)))

            tafs.append(get_taf(icao, date_start, date_end, date_issued, True))
        with open(f'/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/taf/taf{date_start.strftime("%Y-%m-%dT%H:%M:%SZ")}.json', 'w') as outfile:
            json.dump(tafs, outfile, separators=(',', ':'))


def create_6h_spikes():
    min_prhr, max_prhr, mean_prhr, median_prhr, std_prhr = 13173, 17816, 16186.5, 15840.5, 1977.78
    min_forecast, max_forecast, mean_forecast, median_forecast, std_forecast = 0, 510, 60, 60, 40 # minutes


    for hour in range(0, 24, 6):
        tafs = []
        date_start = file_start_date + timedelta(hours=hour)
        for i in range(round(get_random_value(mean_prhr, median_prhr, min_prhr, max_prhr, std_prhr))):
            icao = random.choice(airports)
            date_issued = date_start - timedelta(minutes=round(get_random_value(mean_forecast, median_forecast, min_forecast, max_forecast, std_forecast)))
            date_end = date_start + timedelta(hours=random.choices([24,30], weights=[39829/(39829+10669), 10669/(39829+10669)])[0])

            tafs.append(get_taf(icao, date_start, date_end, date_issued, False))
        file_path = f'/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/taf/taf{date_start.strftime("%Y-%m-%dT%H:%M:%SZ")}.json'
        try:
            with open(file_path, 'r') as infile:
                existing_tafs = json.load(infile)
        except FileNotFoundError:
            existing_tafs = []

        existing_tafs.extend(tafs)

        with open(file_path, 'w') as outfile:
            json.dump(existing_tafs, outfile, separators=(',', ':'))


def get_random_value(mean, median, min, max, std):
    a,b = (min - mean) / std, (max - mean) / std
    return truncnorm.rvs(a, b, loc=mean, scale=std)


def main():
    create_base_layer()
    create_6h_spikes()

    

if __name__ == "__main__":
    main()