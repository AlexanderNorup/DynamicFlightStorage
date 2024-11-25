import json, os, re, argparse, uuid
from datetime import datetime


# Used to sort
def get_date(item):
    return datetime.strptime(item['DateIssued'], '%Y-%m-%dT%H:%M:%SZ')


def get_clean_metar(weather_event):
    clean_event = {}
    try:
        clean_event['ID'] = str(uuid.uuid4())
        clean_event['Ident'] = weather_event['Ident']
        clean_event['DateIssued'] = weather_event['DateIssued']
        clean_event['Lon'] = weather_event['Lon']
        clean_event['Lat'] = weather_event['Lat']
        clean_event['FlightRules'] = weather_event['FlightRules']
        clean_event['Text'] = weather_event['Text']
    except KeyError as e:
        return None
    return clean_event


def get_clean_taf(weather_event):
    clean_event = {}
    try:
        clean_event['ID'] = str(uuid.uuid4())
        clean_event['Ident'] = weather_event['Ident']
        clean_event['Text'] = weather_event['Text']
        clean_event['Period'] = weather_event['Period']
        clean_event['Lon'] = weather_event['Lon']
        clean_event['Lat'] = weather_event['Lat']
        clean_event['DateIssued'] = weather_event['DateIssued']
        clean_event['Conditions'] = []
        for condition in weather_event['Conditions']:
            if ('Change' in condition):
                clean_event['Conditions'].append(
                    {
                        'FlightRules': condition['FlightRules'],
                        'Text': condition['Text'],
                        'Period': condition['Period'],
                        'Change': condition['Change']
                    }
                )
            else:
                clean_event['Conditions'].append(
                    {
                        'FlightRules': condition['FlightRules'],
                        'Text': condition['Text'],
                        'Period': condition['Period']
                    }
                )
    except KeyError as e:
        return None
    return clean_event


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
type_pattern = r"^(taf|metar)"
unique_hours = set()

for filename in os.listdir(directory_path):
    if re.match(pattern, filename):
        prefix = re.match(pattern, filename).group(0)
        unique_hours.add(prefix)

# For each hour run this loop that writes a new file
for hour in unique_hours:
    # Get all files for this hour
    hourly_files = [filename for filename in os.listdir(directory_path) if filename.startswith(hour)]

    unique_updates = {}
    list_to_write = []

    for filename in hourly_files:
        if filename.endswith('.json'):
            file_type = re.match(type_pattern, filename).group(0)
            file_path = os.path.join(directory_path, filename)

            with open(file_path, 'r') as json_file:
                data = json.load(json_file)
            
            # For each object in the file, create ID using the airport identifier and date issued. 
            # Checks if this ID has already been seen and if not, add this object to be written
            for o in data:
                try:
                    id = o['Ident'] + '-' + o['DateIssued']
                    if id not in unique_updates:
                        unique_updates[id] = (o['Text'], 0)
                        if (file_type == 'metar'):
                            clean_event = get_clean_metar(o)
                        elif (file_type == 'taf'):
                            clean_event = get_clean_taf(o)
                        else:
                            continue
                        if(clean_event):
                            list_to_write.append(clean_event)
                    else:
                        if(unique_updates[id][0] != o['Text']):
                            unique_updates[id] = (o['Text'], unique_updates[id][1] + 1)
                except KeyError:
                    continue
                
    file_path = os.path.join(output_directory, f'{hour}.json')

    # Sort based on date, then write
    if len(list_to_write) > 0:
        with open(file_path, 'w') as json_file:
            list_to_write = sorted(list_to_write, key=get_date)
            json.dump(list_to_write, json_file, indent=4)


to_save = {}
for key, value in unique_updates.items():
    if (value[1] > 0):
        to_save[key] = value
json.dump(to_save, open('unique_updates.json', 'w'), indent=4)
