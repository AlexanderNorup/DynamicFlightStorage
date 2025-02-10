import json
import uuid
import random
from datetime import datetime, timedelta

dir_to_save = "flights"

start_date = datetime(2025, 1, 1)
end_date = datetime(2025, 1, 2)

time_step_minutes = 120
flights_per_timestep = 2

# The range of days which the flights are scheduled ahead of time. 
# The flights are caclulated based on their departure 
# and the issued date is then added on afterwards
min_pre_schedule_hours = 1 * 24
max_pre_schedule_hours = 14 * 24

min_flight_length_minutes = int(0.5 * 60)
max_flight_length_minutes = int(4 * 60)

with open('/home/sebastian/Desktop/thesis/DynamicFlightStorage/scripts/FakeDataGeneration/european_airport_pairs.json', 'r') as file:
    airport_pairs = json.load(file)

def create_flight(dep_ICAO, dest_ICAO, date_planned):
    related_dep_ICAO = random.choice([icao for icao in airport_pairs[dep_ICAO] if icao != dep_ICAO and icao != dest_ICAO])
    related_dest_ICAO = random.choice([icao for icao in airport_pairs[dest_ICAO] if icao != dep_ICAO and icao != dest_ICAO])

    departure = date_planned + timedelta(hours=random.randint(min_pre_schedule_hours, max_pre_schedule_hours))
    arrival = departure + timedelta(minutes=random.randint(min_flight_length_minutes, max_flight_length_minutes))

    return {
        "FlightIdentification": str(uuid.uuid4()),
        "DepartureAirport": dep_ICAO,
        "DestinationAirport": dest_ICAO,
        "OtherRelatedAirports":{
            related_dep_ICAO: "AdequateAirport",
            related_dest_ICAO: "AdequateAirport"
        },
        "ScheduledTimeOfDeparture": departure.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "ScheduledTimeOfArrival": arrival.strftime("%Y-%m-%dT%H:%M:%SZ"),
        "DatePlanned": date_planned.strftime("%Y-%m-%dT%H:%M:%SZ")
    }

def main():
    current_date = start_date
    amount_of_flights = (end_date - start_date).total_seconds() / 60 / time_step_minutes * flights_per_timestep
    print("You're about to write " + str(amount_of_flights) + " json files to the folder: " + dir_to_save)
    confirmation = input("Are you sure? y/n: ").strip()
    if confirmation != "y":
        return
    
    while current_date < end_date:
        for i in range(flights_per_timestep):
            dep_ICAO = random.choice(list(airport_pairs.keys()))
            dest_ICAO = random.choice([icao for icao in airport_pairs.keys() if icao != dep_ICAO])
            date_planned = current_date + timedelta(minutes=random.randint(0, time_step_minutes))

            flight = create_flight(dep_ICAO, dest_ICAO, date_planned)
            filename = f"{dir_to_save}/flight{date_planned.strftime('%Y%m%dT%H%M%SZ')}_{flight['FlightIdentification'][:4]}.json"
            with open(filename, 'w') as f:
                json.dump(flight, f, indent=4)

        current_date = current_date + timedelta(minutes=time_step_minutes)

if __name__ == "__main__":
    main()