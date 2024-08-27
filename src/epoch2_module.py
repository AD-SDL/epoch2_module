"""
REST-based node that interfaces with WEI and provides a simple Sleep(t) function
"""

import time
import traceback
from pathlib import Path
from tempfile import NamedTemporaryFile
from typing import Annotated, Optional

from starlette.datastructures import State
from wei.modules.rest_module import RESTModule
from wei.types.module_types import (
    LocalFileModuleActionResult,
    ModuleAction,
    ModuleState,
    ModuleStatus,
)
from wei.types.step_types import StepFailed, StepFileResponse, StepSucceeded
from wei.utils import extract_version

from epoch2_interface.epoch2_interface import Gen5Interface, Gen5

epoch2_module = RESTModule(
    name="epoch2_module",
    version=extract_version(Path(__file__).parent.parent / "pyproject.toml"),
    description="Python WEI module to control the Epoch 2 Platereader",
    model="Epoch 2",
)
epoch2_module.arg_parser.add_argument("--com_port", default=4, type=int, help="The COM Port for the reader to interface with.")

# ***********#
# *Lifecycle*#
# ***********#

@epoch2_module.startup()
def startup_handler(state: State):
    """
    Connects to Gen5 when the module starts up
    """
    state.gen5 = None
    state.experiment = None
    state.plate_read_monitor = None
    state.plate = None
    state.plates = None
    state.gen5 = Gen5Interface(com_port=state.com_port)




@epoch2_module.shutdown()
def shutdown_handler(state: State):
    """
    Disconnects from Gen5 before shutting down the module
    """
    cleanup_experiment(state)
    del state.gen5



@epoch2_module.state_handler()
def state_handler(state: State) -> ModuleState:
    """
    Returns the state of the module
    """

    try:
        reader_status = state.gen5.client.GetReaderStatus()
    except Exception:
        reader_status = None

    return ModuleState.model_validate(
        {
            "status": state.status,  # *Required
            "error": state.error,
            # *Custom state fields
            "reader_status": reader_status,
        }
    )



@staticmethod
def exception_handler(
    state: State, exception: Exception, error_message: Optional[str] = None
):
    """This function is called whenever a module encounters or throws an irrecoverable exception.
    It should handle the exception (print errors, do any logging, etc.) and set the module status to ERROR."""
    if error_message:
        print(f"Error: {error_message}")
    traceback.print_exc()
    state.status = ModuleStatus.ERROR
    state.error = str(exception)
    cleanup_experiment(state)


###########
# Actions #
###########

@epoch2_module.action()
def carrier_in(state: State) -> ModuleAction:
    """
    Moves the carrier in
    """
    state.gen5.client.CarrierIn()
    return StepSucceeded()

@epoch2_module.action()
def carrier_out(state: State) -> ModuleAction:
    """
    Moves the carrier out
    """
    state.gen5.client.CarrierOut()
    return StepSucceeded()

def cleanup_experiment(state: State):
    """
    Cleans up the experiment
    """
    if state.plate_read_monitor is not None:
        if state.plate_read_monitor.ReadInProgress:
            state.plate.AbortRead()
            while state.plate_read_monitor.ReadInProgress:
                time.sleep(10)
    if state.experiment is not None:
        state.experiment.Close()
        state.experiment = None
    state.plate_read_monitor = None
    state.plate = None
    state.plates = None

@epoch2_module.action(results=[LocalFileModuleActionResult(label="experiment_result", description="The result of the experiment")])
def run_experiment(state: State, experiment_file_path: str, return_file: Annotated[bool, "Whether to return the results of the experiment run"] = False) -> ModuleAction:
    """
    Runs an experiment on the Epoch 2
    """
    print(f"Starting experiment {experiment_file_path}")
    state.experiment = Gen5.Experiment(state.gen5.client.OpenExperiment(experiment_file_path))
    if state.experiment is None:
        cleanup_experiment(state)
        return StepFailed(error=f"Failed to open experiment {experiment_file_path}")
    state.plates = Gen5.Plates(state.experiment.Plates)
    if state.plates is None:
        cleanup_experiment(state)
        return StepFailed(error=f"Failed to get plates from experiment {experiment_file_path}")
    elif state.plates.Count != 1:
        cleanup_experiment(state)
        return StepFailed(error=f"Expected 1 plate, got {state.plates.Count}")
    state.plate = Gen5.Plate(state.plates.GetPlate(1))
    state.plate_read_monitor = Gen5.PlateReadMonitor(state.plate.StartRead())
    if state.plate_read_monitor is None:
        cleanup_experiment(state)
        return StepFailed(error="Failed to start plate read")
    while state.plate_read_monitor.ReadInProgress:
        time.sleep(10)
    if state.plate_read_monitor.ErrorsCount > 0:
        error_message = "; ".join([f"[{state.plate_read_monitor.GetErrorCode(i)}] {state.plate_read_monitor.GetErrorMessage(ErrorIndex=i)}" for i in range(state.plate_read_monitor.ErrorsCount)])
        cleanup_experiment(state)
        return StepFailed(error=f"{state.plate_read_monitor.ErrorsCount} error(s) reading plate: {error_message}")

    if bool(return_file):
        try:
            file_export_names = []
            file_export_names = state.plate.GetFileExportNames(False, file_export_names)
            with NamedTemporaryFile(delete=False, delete_on_close=False) as temp_file:
                temp_file.close()
                state.plate.FileExportEx(file_export_names[0], temp_file.name)
                cleanup_experiment(state)
                return StepFileResponse(files={"experiment_result": temp_file.name})
        except Exception as e:
            cleanup_experiment(state)
            return StepFailed(error=f"Failed to export file: {e}")
    else:
        cleanup_experiment(state)
        return StepSucceeded()


################
# Admin Action #
################

@epoch2_module.cancel()
@epoch2_module.pause()
def pause_handler(state: State) -> None:
    """
    Pauses the module
    """
    if state.plate is not None:
        state.plate.AbortRead()

@epoch2_module.resume()
def resume_handler(state: State) -> None:
    """
    Resumes the module
    """
    if state.plate is not None:
        if state.plate_read_monitor is None:
            state.plate_read_monitor = state.plate.ResumeRead()
            state.status = ModuleStatus.BUSY
            return
    state.status = ModuleStatus.IDLE

@epoch2_module.reset()
def reset_handler(state: State) -> None:
    pause_handler(state)
    cleanup_experiment(state)
    state.status = ModuleStatus.IDLE


if __name__ == "__main__":
    epoch2_module.start()
