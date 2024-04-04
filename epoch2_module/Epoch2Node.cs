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

        public Gen5.Application gen5;
        private BTIAutoStacker bTIAutoStacker;
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

            //InitializeEpoch2();
            InitializePlateStacker();

            server = RestServerBuilder.UseDefaults().Build();
            string server_url = "http://" + Hostname + ":" + Port.ToString() + "/";
            Console.WriteLine(server_url);
            server.Prefixes.Clear();
            server.Prefixes.Add(server_url);
            server.Locals.TryAdd("state", state);
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

        private void InitializePlateStacker()
        {
            bTIAutoStacker = new BTIAutoStacker();
            bTIAutoStacker.SetComPort(stackerComPort);
            bTIAutoStacker.OpenComPort(stackerComPort);
            Console.WriteLine(bTIAutoStacker.TestCommunicationWithoutDialog());
            Console.WriteLine(bTIAutoStacker.GetSystemStatus());
            //Console.WriteLine(bTIAutoStacker.ResetStacker());
            //Console.ReadLine();
            //Console.WriteLine(bTIAutoStacker.GetSystemStatus());
            //Console.WriteLine(bTIAutoStacker.HomeAllAxes());
            //Console.WriteLine(bTIAutoStacker.GetSystemStatus());
            //Thread.Sleep(30000);
            //Console.WriteLine(bTIAutoStacker.GetSystemStatus());
            //Console.WriteLine(bTIAutoStacker.ResetStacker());
            //Thread.Sleep(10000);
            //Console.WriteLine("Blah");
            //bTIAutoStacker.PresentPlateOnCarrier();

            bTIAutoStacker.AboutBox();
            //Console.WriteLine(bTIAutoStacker.SetGripperWidth(85598));
            //Console.WriteLine(bTIAutoStacker.SetLidMode(0));
            //Console.WriteLine(bTIAutoStacker.TransferPlateFromOutToIn());
            //Console.WriteLine("Blah2");
            //Console.WriteLine(bTIAutoStacker.PlateFromInstrumentToClaw());
            //Console.WriteLine(bTIAutoStacker.GetSystemStatus());
            //Thread.Sleep(10000);
            Console.ReadLine();
            Console.WriteLine(bTIAutoStacker.GetSystemStatus());
            //Console.WriteLine(bTIAutoStacker.SendPlateToInstrument());
        }
    }
}