import csv
import json

input_file = 'fake_data_generation/Complete.EU.Mixed.csv'
output_file = 'european_airport_pairs.json'

airport_dict = {}

with open(input_file, mode='r') as csvfile:
    csvreader = csv.DictReader(csvfile)

    for row in csvreader:
        if not row['DEPICAO'].startswith(('E', 'L')) or not row['DESTICAO'].startswith(('E', 'L')):
            continue

        if row['DEPICAO'] in airport_dict:
            airport_dict[row['DEPICAO']].append(row['DESTICAO'])
        else:
            airport_dict[row['DEPICAO']] = [row['DESTICAO']]

with open(output_file, mode='w') as jsonfile:
    json.dump(airport_dict, jsonfile, indent=4)