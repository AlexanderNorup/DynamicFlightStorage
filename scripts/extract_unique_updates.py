import json, os
from datetime import datetime

# Used to sort
def get_date(item):
    return datetime.strptime(item['DateIssued'], '%Y-%m-%dT%H:%M:%SZ')

# either 'taf' or 'metar'. Requires that the working directory contains a folder 
# 'metar-taf-speciale' which itself contains 'taf' and 'metar' folders with respective json files
entry_type = 'taf'

# metar2024-08-03T22... -> metar2024-08-04T21...
hour_categories = [f'{entry_type}2024-08-04T{i:02}' for i in range(23)]

hour_categories.extend([
    f'{entry_type}2024-08-03T22',
    f'{entry_type}2024-08-03T23',
])

directory_path = f'metar-taf-speciale/{entry_type}'

# For each hour run this loop that writes a new file
for hour in hour_categories:
    # Get all files for this hour
    hourly_files = [filename for filename in os.listdir(directory_path) if filename.startswith(hour)]

    unique_updates = set()
    list_to_write = []

    for filename in hourly_files:
        if filename.endswith('.json'):
            file_path = os.path.join(directory_path, filename)

            with open(file_path, 'r') as json_file:
                data = json.load(json_file)
            
            # For each object in the file, create ID using the airport identifier and date issued. 
            # Checks if this ID has already been seen and if not, add this object to be written
            for o in data:
                try:
                    id = o['Ident'] + '-' + o['DateIssued']
                    if id not in unique_updates:
                        unique_updates.add(id)
                        list_to_write.append(o)
                except KeyError:
                    continue
                
    file_path = f'{hour}.json'

    # Sort based on date, then write
    if len(list_to_write) > 0:
        with open(file_path, 'w') as json_file:
            list_to_write = sorted(list_to_write, key=get_date)
            json.dump(list_to_write, json_file, indent=4)