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
        private IRestServer server;

        private Gen5.Application gen5;
        private BTIAutoStacker bTIAutoStacker;
        private short stackerComPort = 2;
        private short epochReaderType = 1;
        private short readerComPort = 3;
        private int readerBaudRate = 3600;


        ~Epoch2Node()
        {
            Console.WriteLine("Exiting...");
            //client.Disconnect();
            //client.Close();
            server.Stop();
            bTIAutoStacker.CloseComPort();
        }

        private void OnExecute()
        {

            InitializeEpoch2();
            InitializePlateStacker();

            server = RestServerBuilder.UseDefaults().Build();
            string server_url = "http://" + Hostname + ":" + Port.ToString() + "/";
            Console.WriteLine(server_url);
            server.Prefixes.Clear();
            server.Prefixes.Add(server_url);
            server.Locals.TryAdd("state", state);
            //server.Locals.TryAdd("client", client);
            try
            {
                //server.Start();
                Console.WriteLine("Press enter to stop the server");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void InitializeEpoch2()
        {
            gen5 = new Gen5.Application();
            gen5.ConfigureSerialReader(epochReaderType, readerComPort, readerBaudRate);
            Console.WriteLine(gen5.TestReaderCommunication());
            gen5.CarrierIn();
            gen5.CarrierOut();
        }

        private void InitializePlateStacker()
        {
            bTIAutoStacker = new BTIAutoStacker();
            bTIAutoStacker.SetComPort(stackerComPort);
            bTIAutoStacker.OpenComPort(stackerComPort);
            Console.WriteLine(bTIAutoStacker.TestCommunicationWithoutDialog());
            bTIAutoStacker.HomeAllAxes();
            //bTIAutoStacker.PresentPlateOnCarrier();
        }
    }
}