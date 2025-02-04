import json
import uuid
import random
from datetime import datetime, timedelta

# SETTINGS
file_start_date = datetime(2025, 1, 1)
file_end_date = datetime(2025, 1, 2)

minimum_forecast_hours = 1
maximum_forecast_hours = 24

hours_spacing = 4

flightrule_list = ["VFR"]
changes_list = ["BECOMING", "TEMPORARY"]

with open('europe_airports.json', 'r') as f:
    airports_data = json.load(f)
airports = [airport['ICAO'] for airport in airports_data]


def create_taf_conditions(date_start, date_end):
    conditions = []

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


def main():
    all_tafs = []
    for airport in airports:
        all_tafs.extend(get_taf_for_airport(airport))
    
    with open('taf/taf_data.json', 'w') as outfile:
        json.dump(all_tafs, outfile, indent=4)


if __name__ == "__main__":
    main()