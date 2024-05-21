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
            object result = gen5.OpenExperiment((string) experiment_file_path);
            if (result == null)
            {
                action.result = StepFailed("No experiment file path provided");
                return;
            }
            else
            {
                Console.WriteLine("Opened experiment file");
            }
            Gen5.Experiment experiment = (Gen5.Experiment) result;
            Gen5.Plates plates = (Gen5.Plates)experiment.Plates;
            if (plates == null)
            {
                throw new Exception("No plates defined in experiment");
            }
            else
            {
                Console.Write("Plate Count: ");
                Console.WriteLine(plates.Count);
            }
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
