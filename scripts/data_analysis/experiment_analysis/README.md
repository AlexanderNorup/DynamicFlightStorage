# Experiment Analysis

These messy python scripts are able to produce a bunch of charts and data about the experiments conducted.

The scripts are tested using `Python 3.11.0` and with the packages and their specific versions in `requirements.txt`. These were just the versions of python and the packages that were installed on my machine when this was made. It probably works just as fine with both newer versions of python and the packages. But if you experience any issues, try the versions I have listed here.

## Usage

1. Start by running `data_downloader.py`, possibly changing the `all_experiments_api` variable in the top of the file to point to your own instance.
2. Then run the `data_analyser.py` script
3. Look at the pretty charts and LaTeX files in the new `analysis_summary/` directory.

If you create your own experiments and/or data-stores, add them to the lists in `config.py` to have them included in a sensible manner in the exported LaTeX files.