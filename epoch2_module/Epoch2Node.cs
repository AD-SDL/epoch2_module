using Grapevine;
using McMaster.Extensions.CommandLineUtils;
using Gen5;
using WEI;
using static WEI.ModuleHelpers;

namespace epoch2_module
{
    class Epoch2Node
    {

        public static int Main(string[] args) => CommandLineApplication.Execute<Epoch2Node>(args);

        [Option(Description = "Server Hostname")]
        public string Hostname { get; set; } = "+";

        [Option(Description = "Server Port")]
        public int Port { get; } = 2000;

        [Option(Description = "Whether or not to simulate the instrument")]
        public bool Simulate { get; } = true;

        public string state = ModuleStatus.INIT;
        private IRestServer server = RestServerBuilder.UseDefaults().Build();

        private readonly Epoch2Driver epoch2Driver;

        public Epoch2Node()
        {
            this.epoch2Driver = new();
        }

        public void deconstruct()
        {
            Console.WriteLine("Exiting...");
            server.Stop();
            epoch2Driver.Dispose();
            Console.WriteLine("Exited...");
        }

        private void RunServer()
        {
            server.Prefixes.Clear();
            server.Prefixes.Add("http://" + Hostname + ":" + Port.ToString() + "/");
            server.Locals.TryAdd("state", state);
            server.Locals.TryAdd("epoch2Driver", epoch2Driver);
            server.Start();
        }

        private void OnExecute()
        {
            try
            {
                RunServer();
                epoch2Driver.InitializeEpoch2();
                UpdateModuleStatus(server, ModuleStatus.IDLE);
            }
            catch (Exception ex)
            {
                // Even if we can't connect to the device, keep the REST Server going
                Console.WriteLine(ex.ToString());
                UpdateModuleStatus(server, ModuleStatus.ERROR);
            }
            finally
            {
                Console.WriteLine("Press enter to stop the server");
                Console.ReadLine();
                deconstruct();
            }
        }
    }
}