from urllib.parse import urlparse
import requests
from getpass import getpass
import os
import sys

download_dir=os.path.join(os.path.dirname(__file__),"experiment_data")
auth_username="speciale"

def download_experiment_data(url, auth):
    request = requests.get(url, auth=auth)

    if request.status_code != 200:
        print(f"Invalid password or url. Server replied:\n\n{request.text}\n\n---\n")
        raise Exception("Failed to download data")

    metadata = request.json()
    experimentData = metadata["experimentData"]

    print(f"Successfully found experiment \"{experimentData['experimentRunDescription']}\"")

    experimentFolder = os.path.join(download_dir,experimentData['experimentRunDescription'])
    if not os.path.exists(experimentFolder):
        os.makedirs(experimentFolder)
    metadataFile = os.path.join(experimentFolder,"metadata.json")
    with open(metadataFile, "w") as f:
        f.write(request.text)

    # Download the rest of the data
    links = metadata["links"]
    parsed_url = urlparse(url)
    base_url = f"{parsed_url.scheme}://{parsed_url.hostname}"

    def tryDownloadLink(path, filename):
        global auth
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


if __name__ == "__main__":
    auth_password=""
    if "password" in os.environ:
        auth_password = os.environ["password"]
    else:
        auth_password = getpass("Please enter the secret password: ")

    auth = (auth_username, auth_password)

    url = ""
    if len(sys.argv) > 1:
        url = sys.argv[1]
    else:
        url = input("Please enter the URL to download the data from: ")

    if url.strip() == "":
        raise Exception("URL cannot be blank")
    
    download_experiment_data(url, auth)
