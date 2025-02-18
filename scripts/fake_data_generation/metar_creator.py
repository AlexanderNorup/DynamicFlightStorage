import json
import uuid
import random
from datetime import datetime, timedelta

# SETTINGS
output_dir = '/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/metar'

min_per_hour = 12500
max_per_hour = 14000

day = datetime(2025, 1, 1)

minutes_spacing = 60

flightrules = ['IFR', 'LIFR', 'MVFR', 'VFR']
weights = [0.0354405, 0.02065988, 0.09694729, 0.84704878]

with open('/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/europe_airports.json', 'r') as f:
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


# def get_metar_for_airport(icao):
#     random_minutes = random.randint(0, minutes_spacing)
#     current_date = file_start_date + timedelta(minutes=random_minutes)
#     metar_objects = []
    
#     while current_date < file_end_date:
#         metar_objects.append(create_metar_object(current_date, random.choice(flightrule_list), icao))
#         current_date = current_date + timedelta(minutes=minutes_spacing)
    
#     return metar_objects

def main():
    for i in range(0, 24):
        metars = []
        metar_amount = random.randint(min_per_hour, max_per_hour)
        for j in range(metar_amount):
            date = day + timedelta(hours=i) + timedelta(seconds=random.randint(0, 3599))
            metars.append(create_metar_object(date, random.choices(flightrules, weights=weights)[0], random.choices(airports)[0]))
        with open(f'{output_dir}/metar' + (day + timedelta(hours=i)).strftime("%Y-%m-%dT%H:%M:%SZ") + '.json', 'w') as outfile:
            json.dump(metars, outfile, separators=(',', ':'))


if __name__ == "__main__":
    main()