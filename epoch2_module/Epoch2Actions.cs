using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grapevine;
using WEI;
using static WEI.ModuleHelpers;

namespace epoch2_module
{
    internal class Epoch2Actions
    {

        private readonly IRestServer server;
        private Epoch2Driver epoch2Driver;

        public Epoch2Actions(IRestServer server)
        {
            this.server = server;
            this.epoch2Driver = server.Locals.GetAs<Epoch2Driver>("epoch2Driver");
        }

        public void ActionHandler(ref ActionRequest action)
        {
            Console.WriteLine($"Started handling action: {action.name}; {action.args}");
            //if (epoch2Driver.InProgress)
            //{
            //    action.result = StepFailed("Instrument action in progress");
            //    return;
            //}
            switch (action.name)
            {
                case "carrier_in":
                    epoch2Driver.CarrierIn(ref action);
                    action.result = StepSucceeded("Moved Carrier In");
                    break;
                case "carrier_out":
                    epoch2Driver.CarrierOut(ref action);
                    action.result = StepSucceeded("Move Carrier Out");
                    break;
                case "run_experiment":
                    epoch2Driver.RunExperiment(ref action);
                    action.result = StepSucceeded("Run Experiment");
                    break;
                default:
                    Console.WriteLine("Unknown action: " + action.name);
                    action.result = StepFailed("Unknown action: " + action.name);
                    break;
            }
            Console.WriteLine($"Finished handling action: {action.name}; {action.args}");
        }
    }
}
