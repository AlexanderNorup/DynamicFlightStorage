import os
from io import StringIO
from string import Template
import pandas as pd
from latex_writer import round_if_not_str

template_path=os.path.join(os.path.dirname(__file__),"overview_table_template.tex")
latex_yes="\\cmark"
latex_no="\\xmark"
max_time_diff_for_accept_seconds=5


def get_frame_for_data_store(data_store: str, data_frame: dict[str,list[pd.DataFrame]]) -> dict[str,list[pd.DataFrame]]:
    return dict(filter(lambda x: data_store in x[0], data_frame.items()))

def remove_none_values(input: dict):
    filtered = dict()
    for key, val in input.items():
        if val is not None:
            filtered[key] = val

    return filtered

def get_experiment_index_from_name(experiment_name: str, experiment_order: list[str]) -> int:
    for known_name in experiment_order:
        if experiment_name.lower().startswith(known_name.lower()): # Could realisticly be a contains instead
            return experiment_order.index(known_name)
    
    return -1

def latex_bool(input):
    global latex_no, latex_yes
    if input == True:
        return latex_yes
    elif input == False:
        return latex_no
    else:
        return "?"


def make_overview_table(data_store_names: list[tuple[str,str]],
                        experiment_order: list[str],
                        recalc_frames: dict[str,list[pd.DataFrame]],
                        weather_consumption_frames: dict[str,list[pd.DataFrame]],
                        flight_consumption_frames: dict[str,list[pd.DataFrame]],
                        lag_frames: dict[str,list[pd.DataFrame]],
                        time_frames: dict[str,tuple[int, int]],
                        out_file: str
                        ):
    global template_path, max_time_diff_for_accept_seconds

    table_template = None
    with open(template_path, "r") as f:
        table_template = Template(f.read())

    table_row_template=Template("$name & $maxConsumption & $maxLag & \\multicolumn{1}{l|}{$ex1} & \\multicolumn{1}{l|}{$ex2} & \\multicolumn{1}{l|}{$ex3} & \\multicolumn{1}{l|}{$ex4} & \\multicolumn{1}{l|}{$ex5} & \\multicolumn{1}{l|}{$ex6} & \\multicolumn{1}{l|}{$ex7} & \\multicolumn{1}{l|}{$ex8} & \\multicolumn{1}{l|}{$ex9} & $ex10 & $accurate & \\Cref{$appendix_ref} \\\\ \\hline \n        ")

    print("Making overview table")

    row_writer = StringIO()

    for data_store, data_store_ref_name in data_store_names:
        print(f"Making overview table row for {data_store}")

        #Consumption
        weather_consumptions = get_frame_for_data_store(data_store, weather_consumption_frames)
        max_weather_consumption = max(map(lambda x: float(x.max()), list(weather_consumptions.values())))
        flight_consumptions = remove_none_values(get_frame_for_data_store(data_store, flight_consumption_frames))
        max_flight_consumption = max(map(lambda x: float(x.max()), list(flight_consumptions.values())))
        max_consumption_datastore = max(max_weather_consumption, max_flight_consumption)

        #Lag
        lag_for_datastore = get_frame_for_data_store(data_store, lag_frames)
        flight_lag = max(map(lambda x: float(x["FlightLag"].max()), list(lag_for_datastore.values())))
        weather_lag = max(map(lambda x: float(x["WeatherLag"].max()), list(lag_for_datastore.values())))
        max_lag_datastore = max(flight_lag, weather_lag)

        # Time
        time_for_datastore = get_frame_for_data_store(data_store, time_frames)
        time_result_array = [None] * len(experiment_order)

        for experiment, time in time_for_datastore.items():
            experiment_index = get_experiment_index_from_name(experiment, experiment_order)
            if experiment_index < 0:
                #Ehh, we don't know this one
                print(f"Failed to find index for experiment {experiment}. Did this datastore run this experiment?")
                continue
            
            time_result_array[experiment_index] = abs(time[0] - time[1]) <= max_time_diff_for_accept_seconds

        # Accuracy
        # We need to manually assert this by checking accuracy under load experiment. As of writing this comment, all pass
        accuracy = True 

        row_writer.write(table_row_template.substitute(
            name=data_store,
            maxConsumption=round_if_not_str(max_consumption_datastore),
            maxLag=round_if_not_str(max_lag_datastore),
            ex1=latex_bool(time_result_array[0]),
            ex2=latex_bool(time_result_array[1]),
            ex3=latex_bool(time_result_array[2]),
            ex4=latex_bool(time_result_array[3]),
            ex5=latex_bool(time_result_array[4]),
            ex6=latex_bool(time_result_array[5]),
            ex7=latex_bool(time_result_array[6]),
            ex8=latex_bool(time_result_array[7]),
            ex9=latex_bool(time_result_array[8]),
            ex10=latex_bool(time_result_array[9]),
            accurate=latex_bool(accuracy),
            appendix_ref=f"results:{data_store_ref_name.lower()}"
        ))
    
    table_output = table_template.substitute(table_rows=row_writer.getvalue())
    with open(out_file, "w") as f:
        f.write(table_output)
    print(f"Successfully wrote overview table to file => {out_file}\n")




