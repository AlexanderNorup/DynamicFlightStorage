import json
import uuid
import random
from datetime import datetime, timedelta

# SETTINGS
file_start_date = datetime(2025, 1, 1)
file_end_date = datetime(2025, 1, 2)

minutes_spacing = 60

flightrule_list = ["VFR"]

with open('europe_airports.json', 'r') as f:
    airports_data = json.load(f)
airports = [airport['ICAO'] for airport in airports_data]


def create_metar_object(date, flightrule, ident):
    return {
        "ID": str(uuid.uuid4()),
        "Text": "",
        "DateIssued": date.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "FlightRules": flightrule,
        "Ident": ident
    }


def get_metar_for_airport(icao):
    random_minutes = random.randint(0, minutes_spacing)
    current_date = file_start_date + timedelta(minutes=random_minutes)
    metar_objects = []
    
    while current_date < file_end_date:
        metar_objects.append(create_metar_object(current_date, random.choice(flightrule_list), icao))
        current_date = current_date + timedelta(minutes=minutes_spacing)
    
    return metar_objects

def main():
    all_metars = []
    for airport in airports:
        all_metars.extend(get_metar_for_airport(airport))
    
    with open('metar/metar_data.json', 'w') as outfile:
        json.dump(all_metars, outfile, indent=4)


if __name__ == "__main__":
    main()