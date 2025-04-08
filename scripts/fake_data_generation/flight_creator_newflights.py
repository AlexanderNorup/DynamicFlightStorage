import json
import uuid
import random
from datetime import datetime, timedelta

dir_to_save = "fake_data_generation/flights"

# Weights based on median from data analysis
hours = [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23]
weights = [1117,94,83,791.5,852,775,888.5,747,387.5,755.5,917.5,1166,1446.5,494,980,804,1456,1467,203,889,1323.5,488,41,178]

num_flights = 50_000

day = datetime(2025, 1, 1)

# The range of days which the flights are scheduled ahead of time. 
# The flights are caclulated based on their departure 
# and the issued date is then added on afterwards
min_pre_schedule_hours = 1 * 24
max_pre_schedule_hours = 14 * 24

min_flight_length_minutes = int(0.5 * 60)
max_flight_length_minutes = int(4 * 60)

with open('/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/fake_data_generation/european_airport_pairs.json', 'r') as file:
    airport_pairs = json.load(file)

def create_flight(dep_ICAO, dest_ICAO, date_planned):
    try:
        related_dep_ICAO = random.choice([icao for icao in airport_pairs[dep_ICAO] if icao != dep_ICAO and icao != dest_ICAO])
    except IndexError:
        with open('error_log.txt', 'a') as error_file:
            error_file.write(f"Error finding related_dep_ICAO for dep_ICAO: {dep_ICAO}, dest_ICAO: {dest_ICAO}\n")
        related_dep_ICAO = None

    try:
        related_dest_ICAO = random.choice([icao for icao in airport_pairs[dest_ICAO] if icao != dep_ICAO and icao != dest_ICAO])
    except IndexError:
        with open('error_log.txt', 'a') as error_file:
            error_file.write(f"Error finding related_dest_ICAO for dep_ICAO: {dep_ICAO}, dest_ICAO: {dest_ICAO}\n")
        related_dest_ICAO = None

    departure = date_planned + timedelta(hours=random.randint(min_pre_schedule_hours, max_pre_schedule_hours))
    arrival = departure + timedelta(minutes=random.randint(min_flight_length_minutes, max_flight_length_minutes))

    other_related_airports = {}
    if related_dep_ICAO:
        other_related_airports[related_dep_ICAO] = "AdequateAirport"
    if related_dest_ICAO:
        other_related_airports[related_dest_ICAO] = "AdequateAirport"

    return {
        "FlightIdentification": str(uuid.uuid4()),
        "DepartureAirport": dep_ICAO,
        "DestinationAirport": dest_ICAO,
        "OtherRelatedAirports": other_related_airports,
        "ScheduledTimeOfDeparture": departure.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "ScheduledTimeOfArrival": arrival.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "DatePlanned": date_planned.strftime("%Y-%m-%dT%H:%M:%SZ")
    }

def main():
    print("You're about to write " + str(num_flights) + " json files to the folder: " + dir_to_save)
    confirmation = input("Are you sure? y/n: ").strip()
    if confirmation != "y":
        return
    
    for i in range(num_flights):
        dep_ICAO = random.choice(list(airport_pairs.keys()))
        dest_ICAO = random.choice([icao for icao in airport_pairs.keys() if icao != dep_ICAO])

        dateplanned = day + timedelta(hours=random.choices(hours, weights=weights)[0])
        random_minutes = random.randint(0, 11) * 5
        dateplanned += timedelta(minutes=random_minutes)

        flight = create_flight(dep_ICAO, dest_ICAO, dateplanned)
        filename = f"{dir_to_save}/flight{flight["DatePlanned"]}_{flight['FlightIdentification'][:4]}.json"
        with open(filename, 'w') as f:
            json.dump(flight, f, separators=(',', ':'))

if __name__ == "__main__":
    main()