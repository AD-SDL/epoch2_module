using epoch2_module;
using Grapevine;
using Newtonsoft.Json;
using WEI;
using static WEI.ModuleHelpers;

namespace biostack_module
{
    [RestResource]
    public class BioStackRestServer
    {

        private readonly IRestServer _server;
        private Epoch2Actions _actions;
        public BioStackRestServer(IRestServer server)
        {
            _server = server;
            _actions = new Epoch2Actions(_server);
        }

        [RestRoute("Post", "/admin/reset")]
        public async Task AdminReset(IHttpContext context)
        {
            //var biostack_driver = _server.Locals.GetAs<Epoch2Driver>("biostack_driver");
            //biostack_driver.InProgress = true;
            //biostack_driver.PrintResponse(biostack_driver.stacker.ResetStacker());
            //if (!biostack_driver.CheckAction())
            //{
            //    UpdateModuleStatus(_server, ModuleStatus.ERROR);
            //    await ReturnResult(context, StepFailed($"Failed resetting biostack, error: {biostack_driver.FormatResponseCode(biostack_driver.action_return_code)}"));
            //    return;
            //}
            //UpdateModuleStatus(_server, ModuleStatus.IDLE);
            //await ReturnResult(context, StepSucceeded("Reset complete"));
        }

        [RestRoute("Post", "/admin/home")]
        public async Task AdminHome(IHttpContext context)
        {
            //var biostack_driver = _server.Locals.GetAs<BioStackDriver>("biostack_driver");
            //biostack_driver.InProgress = true;
            //biostack_driver.PrintResponse(biostack_driver.stacker.HomeAllAxes());
            //if (!biostack_driver.CheckAction())
            //{
            //    UpdateModuleStatus(_server, ModuleStatus.ERROR);
            //    await ReturnResult(context, StepFailed($"Failed homing biostack, error: {biostack_driver.FormatResponseCode(biostack_driver.action_return_code)}"));
            //    return;
            //}
            //UpdateModuleStatus(_server, ModuleStatus.IDLE);
            //await ReturnResult(context, StepSucceeded("Reset complete"));
        }

        [RestRoute("Get", "/state")]
        public async Task State(IHttpContext context)
        {
            string state = GetModuleStatus(_server);
            Dictionary<string, string> response = new Dictionary<string, string>
            {
                ["State"] = state,
            };
            Console.WriteLine(state);
            await context.Response.SendResponseAsync(JsonConvert.SerializeObject(response));
        }

        [RestRoute("Get", "/about")]
        public async Task About(IHttpContext context)
        {
            await context.Response.SendResponseAsync((JsonConvert.DeserializeObject(@"
                {
                    ""name"":""Epoch2"",
                    ""model"":""Epoch2 Plate Reader"",
                    ""interface"":""wei_rest_node"",
                    ""version"":""0.1.0"",
                    ""description"":""Module for automating the Epoch2 Plate Reader."",
                    ""actions"": [
                        {""name"":""carrier_in"",""args"":[],""files"":[]},
                        {""name"":""carrier_out"",""args"":[],""files"":[]},
                        {
                            ""name"":""run_experiment"",
                            ""args"": [
                                {
                                    ""name"":""experiment_file_path"",
                                    ""type"":""string"",
                                    ""description"":""Path to the experiment file to run."",
                                    ""required"":true
                                }
                            ],
                            ""files"":[]
                        },
                    ],
                    ""resource_pools"":[],
                    ""admin_command"": []
                }") ?? throw new Exception("Invalid About definition")).ToString()
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
            ActionRequest action;
            try
            {
                action = new ActionRequest(context);
            }
            catch (Exception ex)
            {
                // Problem with the request
                Console.WriteLine(ex.ToString());
                await ReturnResult(context, StepFailed($"Problem processing action request: {ex.Message})"));
                return;
            }

            try
            {
                GetActionLock(_server);

                // Action Definitions for the Module
                _actions.ActionHandler(ref action);

                ReleaseActionLock(_server);
            }
            catch (Exception ex)
            {
                // Unhandled exception while executing the action, module should ERROR
                UpdateModuleStatus(_server, ModuleStatus.ERROR);
                Console.WriteLine(ex.ToString());
                action.result = StepFailed("Step failed: " + ex.Message);
            }

            await action.ReturnResult();
        }
    }

}
