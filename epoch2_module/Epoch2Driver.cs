using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gen5;
using WEI;
using static WEI.ModuleHelpers;

namespace epoch2_module
{
    internal class Epoch2Driver : IDisposable
    {
        private Gen5.Application? gen5;
        private bool _disposedValue;
        private short epochReaderType = 22;
        private int readerBaudRate = 38400;

        public Epoch2Driver()
        {
            gen5 = new Gen5.Application();
        }

        public void InitializeEpoch2(short readerComPort)
        {
            if (gen5 == null)
            {
                gen5 = new Gen5.Application();
            }
            gen5.ConfigureSerialReader(epochReaderType, readerComPort, readerBaudRate);
            if (gen5.TestReaderCommunication() == 1)
            {
                Console.WriteLine("Successfully connected to Epoch2");
            }
            else
            {
                Console.WriteLine("Unable to connect to the reader, check connection and power, and ensure there isn't other software controlling the device.");
            }
        }

        public void CarrierOut(ref ActionRequest action)
        {
            if (gen5 == null)
            {
                gen5 = new Gen5.Application();
            }
            gen5.CarrierOut();
        }

        public void CarrierIn(ref ActionRequest action)
        {
            if (gen5 == null)
            {
                gen5 = new Gen5.Application();
            }
            gen5.CarrierIn();
        }

        public void RunExperiment(ref ActionRequest action)
        {
            Console.WriteLine("RUNNING EXPERIMENT");
            if (gen5 == null)
            {
                gen5 = new Gen5.Application();
            }
            object? experiment_file_path;
            Console.WriteLine(action.args);
            action.args.TryGetValue("experiment_file_path", out experiment_file_path);
            if (experiment_file_path == null)
            {
                action.result = StepFailed("No experiment file path provided");
                return;
            }
            Console.WriteLine(experiment_file_path);
            Gen5.Experiment? experiment;
            try
            {
                experiment = (Gen5.Experiment)gen5.OpenExperiment((string)experiment_file_path);
                if (experiment == null)
                {
                    action.result = StepFailed("No experiment file path provided");
                    return;
                }
                else
                {
                    Console.WriteLine("Opened experiment file");
                }
            } catch (Exception ex)
            {
                action.result = StepFailed("Failed to open experiment file: " + ex.Message);
                return;
            }
            
            Gen5.Plates plates = (Gen5.Plates)experiment.Plates;
            if (plates == null)
            {
                action.result = StepFailed("No plates found in experiment file");
                experiment.Close();
                return;
            }
            else if (plates.Count != 1)
            {
                action.result = StepFailed("Currently only supports one plate per experiment");
                experiment.Close();
                return;
            }

            Gen5.Plate plate = (Gen5.Plate) plates.GetPlate(Index: 1);

            Gen5.PlateReadMonitor plate_read_monitor = (Gen5.PlateReadMonitor) plate.StartRead();
            if (plate_read_monitor == null)
            {
                action.result = StepFailed("Failed to start plate read");
                experiment.Close();
                return;
            }
            else
            {
                Console.WriteLine("Started plate read");
            }

            while (plate_read_monitor.ReadInProgress)
            {
                System.Threading.Thread.Sleep(30000);
            }

            if (plate_read_monitor.ErrorsCount > 0)
            {
                action.result = StepFailed("Errors occurred during plate read: ");
                for (int i = 0; i < plate_read_monitor.ErrorsCount; i++)
                {
                    var error_message = plate_read_monitor.GetErrorMessage(ErrorIndex: i);
                    Console.WriteLine(error_message);
                    action.result["action_log"] = action.result["action_log"] + error_message;
                }
                experiment.Close();
                return;
            }
            else
            {
                Console.WriteLine("Plate read completed.");
            }

            object? file_export_names = null;
            plate.GetFileExportNames(false, ref file_export_names);
            Console.WriteLine(file_export_names);
            string[] file_export_array = (string[])file_export_names;
            string old_temp_file = Path.GetTempFileName();
            string temp_file = Path.ChangeExtension(old_temp_file, ".csv");
            File.Move(old_temp_file, temp_file);
            if (file_export_array.Length == 0)
            {
                Console.WriteLine("No configured file exports");
                Console.WriteLine(temp_file);
                plate.FileExport(temp_file);
                action.result = StepSucceeded(temp_file);
            } else
            {
                // Note: If there are multiple file exports configured, we only return the first one
                Console.WriteLine(file_export_array[0]);
                Console.WriteLine(temp_file);
                plate.FileExportEx(file_export_array[0], temp_file);
                action.result = StepResult(action_response: StepStatus.SUCCEEDED, action_msg: temp_file, action_log: temp_file);
                action.result_is_file = true;
            }
            experiment.Close();
        }

        ~Epoch2Driver() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    gen5 = null;
                    GC.Collect();
                    Console.WriteLine("Disconnected from Epoch2");
                }
                _disposedValue = true;
            }
        }
    }
}
