using Grapevine;
using Newtonsoft.Json;
using System;
using System.Reflection.Metadata.Ecma335;

namespace epoch2_module
{
    public class Epoch2RestServer
    {

        private readonly IRestServer _server;
        public Epoch2RestServer(IRestServer server)
	    {
            _server = server;
	    }

        [RestRoute("Get", "/state")]
        public async Task State(IHttpContext context)
        {
            string state = _server.Locals.GetAs<string>("state");
            Dictionary<string, string> response = new Dictionary<string, string>
            {
                ["State"] = state
            };
            Console.WriteLine(state);
            await context.Response.SendResponseAsync(JsonConvert.SerializeObject(response));
        }

        [RestRoute("Get", "/about")]
        public async Task About(IHttpContext context)
        {
            // TODO
            await context.Response.SendResponseAsync(@"
                {
                    ""name"":""Epoch2"",
                    ""model"":""BioTek Epoch2 Plate Reader"",
                    ""interface"":""wei_rest_node"",
                    ""version"":""0.1.0"",
                    ""description"":""Module for automating the Epoch 2 platereader."",
                    ""actions"": [
                        {""name"":""open"",""args"":[],""files"":[]},
                        {""name"":""close"",""args"":[],""files"":[]},
                        {""name"":""run_assay"",""args"":[{""name"":""assay_name"",""type"":""str"",""default"":null,""required"":true,""description"":""Name of the assay to run""}],""files"":[]}
                    ],
                    ""resource_pools"":[]
                }"
            );
        }

        [RestRoute("Get", "/resources")]
        public async Task Resources(IHttpContext context)
        {
            // TODO
            await context.Response.SendResponseAsync("resources");
        }

        [RestRoute("Post", "/action")]
        public async Task Action(IHttpContext context)
        {
            string action_handle = context.Request.QueryString["action_handle"];
            string action_vars = context.Request.QueryString["action_vars"];
            Dictionary<string, string> args = JsonConvert.DeserializeObject<Dictionary<string, string>>(action_vars);
            var result = UtilityFunctions.action_response();
            string state = _server.Locals.GetAs<string>("state");
            //HidexSenseAutomationServiceClient client = _server.Locals.GetAs<HidexSenseAutomationServiceClient>("client");

            if (state == ModuleStatus.BUSY)
            {
                result = UtilityFunctions.action_response(StepStatus.FAILED, "", "Module is Busy");
                await context.Response.SendResponseAsync(JsonConvert.SerializeObject(result));
            }
            try
            {
                _server.Locals.TryUpdate("state", ModuleStatus.BUSY, _server.Locals.GetAs<string>("state"));
                switch (action_handle)
                {
                    case "blah":
                        break;
                    default:
                        Console.WriteLine("Unknown action: " + action_handle);
                        result = UtilityFunctions.action_response(StepStatus.FAILED, "", "Unknown action: " + action_handle);
                        break;
                }
                _server.Locals.TryUpdate("state", ModuleStatus.IDLE, _server.Locals.GetAs<string>("state"));
            }
            catch (Exception ex)
            {
                _server.Locals.TryUpdate("state", ModuleStatus.ERROR, _server.Locals.GetAs<string>("state"));
                Console.WriteLine(ex.ToString());
                result = UtilityFunctions.action_response(StepStatus.FAILED, "", "Step failed: " + ex.ToString());
            }

            await context.Response.SendResponseAsync(JsonConvert.SerializeObject(result));
        }
    }

}
