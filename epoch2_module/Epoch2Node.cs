using Grapevine;
using McMaster.Extensions.CommandLineUtils;
using Gen5;
using BTIAUTOSTACKERLib;

namespace epoch2_module
{
    class Epoch2Node
    {

        public static int Main(string[] args) => CommandLineApplication.Execute<Epoch2Node>(args);

        [Option(Description = "Server Hostname")]
        public string Hostname { get; set; } = "+";

        [Option(Description = "Server Port")]
        public int Port { get; } = 2000;

        [Option(Description = "Whether or not to simulate the instrument (note: if the instrument is connected, this does nothing)")]
        public bool Simulate { get; } = true;

        public string state = ModuleStatus.INIT;
        private IRestServer server = RestServerBuilder.UseDefaults().Build();

        public Gen5.Application? gen5;
        private short stackerComPort = 5;
        private short epochReaderType = 22;
        private short readerComPort = 4;
        private int readerBaudRate = 38400;


        public void deconstruct()
        {
            Console.WriteLine("Exiting...");
            server.Stop();
            bTIAutoStacker.CloseComPort();
            gen5 = null;
            GC.Collect();
            Console.WriteLine("Exited...");
        }

        private void OnExecute()
        {

            InitializeEpoch2();

            string server_url = "http://" + Hostname + ":" + Port.ToString() + "/";
            Console.WriteLine(server_url);
            server.Prefixes.Clear();
            server.Prefixes.Add(server_url);
            server.Locals.TryAdd("state", state);
            try
            {
                server.Start();
                Console.WriteLine("Press enter to stop the server");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                deconstruct();
            }
        }

        private void InitializeEpoch2()
        {
            gen5 = new Gen5.Application();
            gen5.ConfigureSerialReader(epochReaderType, readerComPort, readerBaudRate);
            Console.WriteLine(gen5.TestReaderCommunication());
            gen5.CarrierOut();
            Thread.Sleep(5000);
            gen5.CarrierIn();
        }
    }
}