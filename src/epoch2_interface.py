"""Interface for controlling the epoch2 device/instrument/robot."""

import gc
import time
from datetime import datetime
from pathlib import WindowsPath
from tempfile import NamedTemporaryFile
from typing import Optional

import clr

clr.AddReference(
    r"C:\\Program Files\\Agilent\\Gen5 3.15\\OLE Automation\\Samples\\C# Sample\\obj\\Debug\\Interop.Gen5.dll"
)
import Gen5  # noqa
from System import GC  # noqa

# * Using .dlls and .NET assemblies
# * pip install pythonnet
# * See docs: https://pythonnet.github.io/pythonnet/python.html


class Gen5Interface:
    """Interface for the Epoch2 Platereader via Gen5"""

    experiment: Optional[Gen5.Experiment] = None
    plate_read_monitor: Optional[Gen5.PlateReadMonitor] = None
    plates: Optional[Gen5.Plates] = None
    plate: Optional[Gen5.Plate] = None
    client: Optional[Gen5.Application] = None
    status: int = 0
    """
    eReaderStatus_OK (0) indicates the reader is ready and has no current error status
    eReaderStatus_Busy (-1) indicates the reader is busy
    eReaderStatus_NotCommunicating (-2) indicates the reader is not communicating
    eReaderStatus_NotConfigured (-3) indicates no reader has been configured, else,
    <positive number> is an error status returned by the reader
        indicating a reboot or system test is required. The returned value
        should be interpreted as an unsigned 16-bit hex code. See the reader
        manual for details regarding error statuses
    """

    def __init__(self, com_port=4, reader_type=22, baud_rate=38400):
        """Initialize the interface to the Epoch 2"""
        self.client = Gen5.Application()
        self.client.ConfigureSerialReader(reader_type, com_port, baud_rate)
        comms_test = self.client.TestReaderCommunication()
        print(
            "Communications passed"
            if comms_test == 1
            else f"Communications test returned error code: {comms_test}"
        )
        self.get_reader_status()

    def get_reader_status(self):
        """Get the status data from the Reader"""
        self.status = self.client.GetReaderStatus()
        return self.status

    def carrier_in(self):
        """Moves the plate carrier in"""
        self.client.CarrierIn()

    def carrier_out(self):
        """Moves the plate carrier out"""
        self.client.CarrierOut()

    def cleanup_experiment(self):
        """
        Cleans up an experiment
        """
        print("Cleaning up experiment")
        if self.plate_read_monitor is not None:
            if self.plate_read_monitor.ReadInProgress:
                self.plate.AbortRead()
                while self.plate_read_monitor.ReadInProgress:
                    time.sleep(1)
        if self.experiment is not None:
            self.experiment.Close()
            self.experiment = None
        self.plate_read_monitor = None
        self.plate = None
        self.plates = None
        print("Cleanup complete")

    def run_experiment(
        self, experiment_file_path: WindowsPath, return_file: bool = False
    ) -> tuple[bool, Optional[WindowsPath]]:
        """
        Runs an experiment on the Epoch 2
        """
        try:
            print(f"Starting experiment {experiment_file_path}")
            experiment_path_str = str(experiment_file_path)
            self.experiment = Gen5.Experiment(
                self.client.OpenExperiment(experiment_path_str)
            )
            if self.experiment is None:
                raise FileNotFoundError(
                    f"Failed to open experiment {experiment_path_str}"
                )
            self.plates = Gen5.Plates(self.experiment.Plates)
            if self.plates is None:
                raise ValueError(
                    f"Failed to get plates from experiment {experiment_path_str}"
                )
            if self.plates.Count != 1:
                raise ValueError(f"Expected 1 plate, got {self.plates.Count}")
            self.plate = Gen5.Plate(self.plates.GetPlate(1))
            self.plate_read_monitor = Gen5.PlateReadMonitor(self.plate.StartRead())
            if self.plate_read_monitor is None:
                raise RuntimeError("Failed to start plate read")
            last_print_time = time.time()
            time.time()
            while self.plate_read_monitor.ReadInProgress:
                if time.time() - last_print_time > 60:
                    print(f"Read in progress as of {datetime.now().astimezone()}")
                    last_print_time = time.time()
                time.sleep(1)
            if self.plate_read_monitor.ErrorsCount > 0:
                error_message = "; ".join(
                    [
                        f"[{self.plate_read_monitor.GetErrorCode(i)}] {self.plate_read_monitor.GetErrorMessage(ErrorIndex=i)}"
                        for i in range(self.plate_read_monitor.ErrorsCount)
                    ]
                )
                raise RuntimeError(
                    f"{self.plate_read_monitor.ErrorsCount} error(s) reading plate: {error_message}"
                )

            if bool(return_file):
                file_export_names = []
                file_export_names = self.plate.GetFileExportNames(
                    False, file_export_names
                )
                with NamedTemporaryFile(
                    delete=False, delete_on_close=False
                ) as temp_file:
                    temp_file.close()
                    self.plate.FileExportEx(file_export_names[0], temp_file.name)
                    return True, WindowsPath(temp_file.name)
            else:
                return True, None
        except KeyboardInterrupt:
            print("Interrupted by user, aborting experiment!")
            print("⚠️⚠️⚠️DO NOT INTERRUPT THIS PROCESS, IT MAY TAKE 30+ SECONDS!⚠️⚠️⚠️")
            print(
                "⚠️⚠️⚠️If prompted by the Jupyter Kernel to Restart, DON'T! (click 'cancel' to dismiss the dialog)⚠️⚠️⚠️"
            )
            return False, None
        finally:
            self.cleanup_experiment()

    def __del__(self):
        """Attempt to cleanly shutdown and disconnect from Epoch 2"""
        self.cleanup_experiment()
        del self.client
        self.client = None
        GC.Collect()
        GC.WaitForPendingFinalizers()
        gc.collect()

    def cancel(self):
        """Attempt to cancel any running reads"""
        self.cleanup_experiment()

    def resume(self):
        """Attempt to resume a paused run"""
        if self.plate is not None:
            if self.plate_read_monitor is None:
                self.plate_read_monitor = self.plate.ResumeRead()
                return


if __name__ == "__main__":
    gen5 = Gen5Interface()
    del gen5
