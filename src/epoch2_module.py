"""
REST-based node that interfaces with MADsci to control the Epoch2 Platereader
"""

import traceback
from pathlib import Path, WindowsPath
from typing import Annotated, Optional

from madsci.common.types.base_types import Error
from madsci.node_module.helpers import action
from madsci.node_module.rest_node_module import RestNode, RestNodeConfig

from epoch2_interface import Gen5Interface


class Epoch2NodeConfig(RestNodeConfig):
    """Configuration for the Epoch2Node module"""

    com_port: int = 4


class Epoch2Node(RestNode):
    """Python MADSci module to control the Epoch 2 Platereader"""

    config: Epoch2NodeConfig = Epoch2NodeConfig()
    config_model = Epoch2NodeConfig
    epoch2: Optional[Gen5Interface] = None

    # ***********#
    # *Lifecycle*#
    # ***********#

    def startup_handler(self) -> None:
        """
        Connects to Gen5 when the module starts up
        """
        self.epoch2 = Gen5Interface(com_port=self.config.com_port, logger=self.logger)

    def shutdown_handler(self) -> None:
        """
        Disconnects from Gen5 before shutting down the module
        """
        del self.epoch2
        self.epoch2 = None

    def state_handler(self) -> None:
        """
        Returns the state of the module
        """
        self.node_state = {"reader_status": self.epoch2.get_reader_status()}

    def exception_handler(
        self, exception: Exception, error_message: Optional[str] = None
    ) -> None:
        """This function is called whenever a module encounters or throws an irrecoverable exception.
        It should handle the exception (print errors, do any logging, etc.) and set the module status to ERROR."""
        if error_message:
            self.logger.error(f"Error: {error_message}")
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
        self.epoch2.carrier_in()

    @action
    def carrier_out(self) -> None:
        """
        Moves the carrier out
        """
        self.epoch2.carrier_out()

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
        _, file = self.epoch2.run_experiment(
            experiment_file_path=WindowsPath(experiment_file_path),
            return_file=return_file,
        )
        return Path(file)

    ################
    # Admin Action #
    ################

    def cancel(self) -> None:
        """
        Attempt to cancel the current run
        """
        self.epoch2.cancel()

    def resume(self) -> None:
        """Attempt to resume the current plate read"""
        self.epoch2.resume()


if __name__ == "__main__":
    epoch_node = Epoch2Node()
    epoch_node.start_node()
