import json

with open("fake_data_generation/main_Airport.json") as json_file:
    data = json.load(json_file)

eu_airports = []

for o in data:
    try:
        icao = o["ICAO"]
        if icao.startswith(("L", "E")):
            eu_airports.append(o)
    except KeyError:
        continue

with open("europe_airports.json", "w") as json_file:
    json.dump(eu_airports, json_file, indent=4)