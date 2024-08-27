"""Interface for controlling the epoch2 device/instrument/robot."""

from pathlib import Path
import gc
import clr
clr.AddReference(r"C:\\Program Files\\Agilent\\Gen5 3.15\\OLE Automation\\Samples\\C# Sample\\obj\\Debug\\Interop.Gen5.dll")
import Gen5
from System import GC

from starlette.datastructures import State

# * Using .dlls and .NET assemblies
# * pip install pythonnet
# * See docs: https://pythonnet.github.io/pythonnet/python.html


class Gen5Interface:
    def __init__(self, com_port=4, reader_type = 22, baud_rate = 38400):
        self.client = Gen5.Application()
        self.client.ConfigureSerialReader(reader_type, com_port, baud_rate)
        print(self.client.TestReaderCommunication())
    
    def __del__(self):
        del self.client
        self.client = None
        GC.Collect()
        GC.WaitForPendingFinalizers()
        gc.collect(generation=2)




if __name__ == "__main__":
    gen5 = Gen5Interface()
    del gen5

