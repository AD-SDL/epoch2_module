using System;
using System.Collections.Generic;
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
        private short readerComPort = 4;
        private int readerBaudRate = 38400;

        public Epoch2Driver()
        {
            gen5 = new Gen5.Application();
        }

        public void InitializeEpoch2()
        {
            if (gen5 == null)
            {
                gen5 = new Gen5.Application();
            }
            gen5.ConfigureSerialReader(epochReaderType, readerComPort, readerBaudRate);
            Console.WriteLine(gen5.TestReaderCommunication());
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
            object experiment_file_path;
            action.args.TryGetValue("experiment_file_path", out experiment_file_path);
            if (experiment_file_path == null)
            {
                action.result = StepFailed("No experiment file path provided");
                return;
            }
            Console.WriteLine(experiment_file_path);
            Gen5.Experiment experiment = (Gen5.Experiment) gen5.OpenExperiment((string) experiment_file_path);
            if (experiment == null)
            {
                action.result = StepFailed("No experiment file path provided");
                return;
            }
            else
            {
                Console.WriteLine("Opened experiment file");
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
                System.Threading.Thread.Sleep(1000);
            }

            if (plate_read_monitor.ErrorsCount > 0)
            {
                action.result = StepFailed("Errors occurred during plate read");
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
            experiment.Close();
            action.result = StepSucceeded("Experiment loaded");
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
                }
                _disposedValue = true;
            }
        }
    }
}
