"""
REST-based node that interfaces with WEI and provides a simple Sleep(t) function
"""

import time
import traceback
from pathlib import Path
from tempfile import NamedTemporaryFile
from typing import Annotated, Optional
from madsci.node_module.rest_node_module import RestNode, RestNodeConfig
from madsci.common.types.base_types import Error
from epoch2_interface import Gen5, Gen5Interface
from madsci.node_module.helpers import action
from madsci.common.types.action_types import ActionFailed

class Epoch2NodeConfig(RestNodeConfig):
    """Configuration for the Epoch2Node module"""

    com_port: int = 4

class Epoch2Node(RestNode):
    """Python WEI module to control the Epoch 2 Platereader"""
    config = Epoch2NodeConfig()
    config_model = Epoch2NodeConfig

# ***********#
# *Lifecycle*#
# ***********#


    def startup_handler(self):
        """
        Connects to Gen5 when the module starts up
        """
        self.gen5 = None
        self.experiment = None
        self.plate_read_monitor = None
        self.plate = None
        self.plates = None
        self.gen5 = Gen5Interface(com_port=self.config.com_port)



    def shutdown_handler(self):
        """
        Disconnects from Gen5 before shutting down the module
        """
        self.cancel()
        self.cleanup_experiment()
        del self.gen5
        self.gen5 = None





    def state_handler(self) -> dict:
        """
        Returns the state of the module
        """

        try:
            reader_status = self.gen5.client.GetReaderStatus()
        except Exception as e:
            reader_status = None
            raise e

        return {"reader_status": reader_status}
            


    def exception_handler(
        self, exception: Exception, error_message: Optional[str] = None
    ):
        """This function is called whenever a module encounters or throws an irrecoverable exception.
        It should handle the exception (print errors, do any logging, etc.) and set the module status to ERROR."""
        if error_message:
            print(f"Error: {error_message}")
        traceback.print_exc()
        self.node_status.errored = True
        self.node_status.errors.append(Error(str(exception))) 
        self.cleanup_experiment()


    ###########
    # Actions #
    ###########


    @action
    def carrier_in(self) -> None:
        """
        Moves the carrier in
        """
        self.gen5.client.CarrierIn()


    @action
    def carrier_out(self) -> None:
        """
        Moves the carrier out
        """
        self.gen5.client.CarrierOut()


    def cleanup_experiment(self):
        """
        Cleans up the experiment
        """
        if self.plate_read_monitor is not None:
            if self.plate_read_monitor.ReadInProgress:
                self.plate.AbortRead()
                while self.plate_read_monitor.ReadInProgress:
                    time.sleep(10)
        if self.experiment is not None:
            self.experiment.Close()
            self.experiment = None
        self.plate_read_monitor = None
        self.plate = None
        self.plates = None


    @action
    def run_experiment(
        self,
        experiment_file_path: str,
        return_file: Annotated[
            bool, "Whether to return the results of the experiment run"
        ] = False,
    ) -> Annotated[Path, "The experiment result file"]:
        """
        Runs an experiment on the Epoch 2
        """
        print(f"Starting experiment {experiment_file_path}")
        self.experiment = Gen5.Experiment(
            self.gen5.client.OpenExperiment(experiment_file_path)
        )
        if self.experiment is None:
            self.cleanup_experiment()
            return ActionFailed(error=f"Failed to open experiment {experiment_file_path}")
        self.plates = Gen5.Plates(self.experiment.Plates)
        if self.plates is None:
            self.cleanup_experiment()
            return ActionFailed(
                errors=[f"Failed to get plates from experiment {experiment_file_path}"]
            )
        elif self.plates.Count != 1:
            self.cleanup_experiment()
            return ActionFailed(errors=[f"Expected 1 plate, got {self.plates.Count}")]
        self.plate = Gen5.Plate(self.plates.GetPlate(1))
        self.plate_read_monitor = Gen5.PlateReadMonitor(self.plate.StartRead())
        if self.plate_read_monitor is None:
            self.cleanup_experiment()
            return ActionFailed(errors=["Failed to start plate read"])
        while self.plate_read_monitor.ReadInProgress:
            time.sleep(10)
        if self.plate_read_monitor.ErrorsCount > 0:
            error_message = "; ".join(
                [
                    f"[{self.plate_read_monitor.GetErrorCode(i)}] {self.plate_read_monitor.GetErrorMessage(ErrorIndex=i)}"
                    for i in range(self.plate_read_monitor.ErrorsCount)
                ]
            )
            self.cleanup_experiment()
            return ActionFailed(
                errors=[f"{self.plate_read_monitor.ErrorsCount} error(s) reading plate: {error_message}"]
            )

        if bool(return_file):
            try:
                file_export_names = []
                file_export_names = self.plate.GetFileExportNames(False, file_export_names)
                with NamedTemporaryFile(delete=False, delete_on_close=False) as temp_file:
                    temp_file.close()
                    self.plate.FileExportEx(file_export_names[0], temp_file.name)
                    self.cleanup_experiment()
                    return Path(temp_file.name)
            except Exception as e:
                self.cleanup_experiment()
                raise e
        else:
            self.cleanup_experiment()


    ################
    # Admin Action #
    ################

    def cancel(self) -> None:
        """
        cancels the current run
        """
        if self.plate is not None:
            self.plate.AbortRead()



if __name__ == "__main__":
    epoch_node = Epoch2Node()
    epoch_node.start_node()
