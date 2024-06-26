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

        [Option(Description = "The COM Port of the reader to control")]
        public short COMPort { get; } = 4;

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
            Console.WriteLine("REST Server running on port {0}", Port.ToString());
        }

        private void OnExecute()
        {
            try
            {
                RunServer();
                Console.WriteLine("COM Port: " + COMPort.ToString());
                epoch2Driver.InitializeEpoch2(COMPort);
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