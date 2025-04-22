from urllib.parse import urlparse
import requests
from getpass import getpass
import os
import sys

download_dir=os.path.join(os.path.dirname(__file__),"experiment_data")
auth_username="speciale"
all_experiments_api="https://dynamicflightstorage.app.alexandernorup.com/api/experiment/"

force_redownload=False


def getbaseurl(url):
    parsed_url = urlparse(url)
    return f"{parsed_url.scheme}://{parsed_url.hostname}"

def download_experiment_data(url, auth):
    global force_redownload
    request = requests.get(url, auth=auth)

    if request.status_code != 200:
        print(f"Invalid password or url. Server replied:\n\n{request.text}\n\n---\n")
        raise Exception("Failed to download data")

    metadata = request.json()
    experimentData = metadata["experimentData"]
    
    print(f"Successfully found experiment \"{experimentData['experimentRunDescription']}\"")

    if experimentData['utcEndTime'] == None or not experimentData['experimentSuccess']:
        print("This experiment is either failed or not done yet. Refusing to work on this one...")
        return

    experimentFolder = os.path.join(download_dir,experimentData['experimentRunDescription'])
    if not os.path.exists(experimentFolder):
        os.makedirs(experimentFolder)
    elif not force_redownload:
        # Path already exists and we don't force redownload.
        print(f"Experiment {experimentData['experimentRunDescription']} already exists locally. Skipping for now because force_redownload is set to False")
        return
    
    metadataFile = os.path.join(experimentFolder,"metadata.json")
    with open(metadataFile, "w") as f:
        f.write(request.text)

    # Download the rest of the data
    links = metadata["links"]
    base_url = getbaseurl(url)

    def tryDownloadLink(path, filename):
        download_url = base_url + path
        print(f"\nDownloading from {download_url}")
        response = requests.get(download_url, auth=auth)
        resultFile = os.path.join(experimentFolder,filename)
        if response.status_code == 200:
            with open(resultFile, "w") as f:
                f.write(response.text)
            print(f"Successfully downloaded {filename}")
        else:
            print(f"Failed to GET {path}. Server replied:\n\n{response.text}\n\n---\n")

    if not links["flightLogs"] == None:
        tryDownloadLink(links["flightLogs"], "flightlog.csv")

    if not links["weatherLogs"] == None:
        tryDownloadLink(links["weatherLogs"], "weatherLog.csv")

    tryDownloadLink(links["lagLogs"], "lagLog.csv")

    tryDownloadLink(links["recalculationLogs"], "recalculationLog.csv")

    print(f"Download completed => {experimentFolder}")


def main():
    auth_password=""
    if "password" in os.environ:
        auth_password = os.environ["password"]
    else:
        auth_password = getpass("Please enter the secret password: ")

    auth = (auth_username, auth_password)

    url = ""
    if len(sys.argv) > 1:
        url = sys.argv[1]

    if url.strip() != "":
        download_experiment_data(url, auth)
        return

    print("Since no URL was specified, downloading all experiments")

    baseurl = getbaseurl(all_experiments_api)
    request = requests.get(all_experiments_api, auth=auth)
    
    if request.status_code != 200:
        print(f"Invalid password or url. Server replied with status {request.status_code}:\n\n{request.text}\n\n---\n")
        raise Exception("Failed to download experiment list")
    
    experimentList = request.json()
    print(f"Found {len(experimentList)} experiments to fetch")
    links = list(map(lambda x: baseurl + x['link'], experimentList))

    for link in links:
        download_experiment_data(link, auth)

if __name__ == "__main__":
    main()
    
    
    
