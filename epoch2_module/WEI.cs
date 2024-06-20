using System;
using Grapevine;
using Newtonsoft.Json;

namespace WEI
{
    public static class ModuleStatus
    {
        public const string
            INIT = "INIT",
            IDLE = "IDLE",
            READY = "READY", // Same as IDLE
            BUSY = "BUSY",
            ERROR = "ERROR",
            UNKNOWN = "UNKNOWN";
    }

    public static class StepStatus
    {
        public const string
            IDLE = "idle",
            RUNNING = "running",
            SUCCEEDED = "succeeded",
            FAILED = "failed";
    }

    /// <summary>
    /// Represents an incoming action request and related context, results, etc.
    /// </summary>
    public class ActionRequest
    {
        public string name = "";
        public Dictionary<string, object> args;
        public Dictionary<string, string> result = ModuleHelpers.StepResult();
        public bool result_is_file = false;
        IHttpContext? action_context;

        /// <summary>
        /// Creates a new object by extracting the <c>action_handle</c> and <c>action_vars</c> query parameters from the <c>context.Request</c>
        /// </summary>
        /// <param name="context"></param>
        /// <exception cref="Exception"></exception>
        public ActionRequest(IHttpContext context)
        {
            action_context = context;
            name = context.Request.QueryString["action_handle"] ?? throw new Exception("Expected an action_handle, but none was provided.");
            string action_vars = context.Request.QueryString["action_vars"] ?? "{}";
            args = JsonConvert.DeserializeObject<Dictionary<string, object>>(action_vars) ?? new Dictionary<string, object>();
        }

        public ActionRequest(string action_handle, Dictionary<string, object> action_vars)
        {
            name = action_handle;
            args = action_vars;
        }

        public async Task ReturnResult()
        {
            if (action_context is null)
            {
                throw new Exception("Attempted to return result without http context.");
            }
            if (result_is_file)
            {
                action_context.Response.Headers.Add("x-wei-action_response", result["action_response"]);
                action_context.Response.Headers.Add("x-wei-action_log", result["action_log"]);
                action_context.Response.Headers.Add("x-wei-action_msg", result["action_msg"]);

                FileStream fs;
                try
                {
                    fs = File.OpenRead(result["action_msg"]);
                    BinaryReader binaryReader = new BinaryReader(fs);
                    var file_bytes = binaryReader.ReadBytes((int)fs.Length);
                    await action_context.Response.SendResponseAsync(file_bytes);
                    return;
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Error returning file {ex.Message}");
                }
            }
            await action_context.Response.SendResponseAsync(JsonConvert.SerializeObject(result));
        }

        public async Task ReturnResult(Dictionary<string, string> result)
        {
            if (action_context is null)
            {
                throw new Exception("Attempted to return result without http context.");
            }
            await action_context.Response.SendResponseAsync(JsonConvert.SerializeObject(result));
        }
    }

    public static class ModuleHelpers
    {
        public static Dictionary<string, string> StepResult(string action_response = StepStatus.IDLE, string action_msg = "", string action_log = "")
        {
            Dictionary<string, string> response = new Dictionary<string, string>()
            {
                ["action_response"] = action_response,
                ["action_msg"] = action_msg,
                ["action_log"] = action_log,
            };
            return response;
        }

        public static Dictionary<string, string> StepSucceeded(string result = "")
        {
            return StepResult(action_response: StepStatus.SUCCEEDED, action_msg: result);
        }

        public static Dictionary<string, string> StepFailed(string reason = "")
        {
            Console.WriteLine(reason);
            return StepResult(action_response: StepStatus.FAILED, action_log: reason);
        }

        public static string GetModuleStatus(IRestServer server)
        {
            return server.Locals.GetAs<string>("state");
        }

        public static void UpdateModuleStatus(IRestServer server, string status)
        {
            server.Locals.TryUpdate("state", status, GetModuleStatus(server));
        }

        public static async Task ReturnResult(IHttpContext httpContext, Dictionary<string, string> result)
        {
            await httpContext.Response.SendResponseAsync(JsonConvert.SerializeObject(result));
        }

        public static void GetActionLock(IRestServer server)
        {
            string status = server.Locals.GetAs<string>("state");
            if (status != ModuleStatus.IDLE && status != ModuleStatus.READY)
            {
                throw new Exception($"Couldn't run action because Module Status is currently {status}");
            }
            UpdateModuleStatus(server, ModuleStatus.BUSY);
        }

        public static void ReleaseActionLock(IRestServer server)
        {
            string status = server.Locals.GetAs<string>("state");
            if (status == ModuleStatus.BUSY)
            {
                UpdateModuleStatus(server, ModuleStatus.IDLE);
            }
        }

        public static void WaitUntilNotBusy(IRestServer server)
        {
            string status = server.Locals.GetAs<string>("state");
            while (status == ModuleStatus.BUSY)
            {
                Thread.Sleep(500);
            }
        }

        public static bool CheckActionSuccess(ref ActionRequest action)
        {
            return (action.result["action_response"] == StepStatus.SUCCEEDED);
        }

    }

}