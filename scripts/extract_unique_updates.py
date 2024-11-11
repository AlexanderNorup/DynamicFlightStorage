import json, os, re, sys, argparse
from datetime import datetime


# Used to sort
def get_date(item):
    return datetime.strptime(item['DateIssued'], '%Y-%m-%dT%H:%M:%SZ')


parser = argparse.ArgumentParser()
parser.add_argument("-d", "--Directory", help = "Directory to read from")
parser.add_argument("-o", "--Output", help = "Output directory (defaults to .)")
args = parser.parse_args()

if (args.Directory):
    directory_path = args.Directory
else:
    directory_path = '.'

if (args.Output):
    output_directory = args.Output
else:
    output_directory = '.'

pattern = r"^(taf|metar)\d{4}-\d{2}-\d{2}T\d{2}"
unique_hours = set()

for filename in os.listdir(directory_path):
    if re.match(pattern, filename):
        prefix = re.match(pattern, filename).group(0)
        unique_hours.add(prefix)

# For each hour run this loop that writes a new file
for hour in unique_hours:
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
                
    file_path = os.path.join(output_directory, f'{hour}.json')

    # Sort based on date, then write
    if len(list_to_write) > 0:
        with open(file_path, 'w') as json_file:
            list_to_write = sorted(list_to_write, key=get_date)
            json.dump(list_to_write, json_file, indent=4)