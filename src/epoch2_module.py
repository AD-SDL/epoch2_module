"""
REST-based node that interfaces with WEI and provides a simple Sleep(t) function
"""

from pathlib import Path
from tempfile import NamedTemporaryFile

from fastapi.datastructures import UploadFile
from starlette.datastructures import State
from typing_extensions import Annotated
from wei.modules.rest_module import RESTModule
from wei.types.module_types import ModuleAction, ModuleActionArg, ModuleState
from wei.types.step_types import (
    ActionRequest,
    StepFileResponse,
    StepResponse,
    StepStatus,
)
from wei.utils import extract_version

from epoch2_interface.epoch2_interface import Gen5Interface

epoch2_module = RESTModule(
    name="epoch2_module",
    version=extract_version(Path(__file__).parent.parent / "pyproject.toml"),
    description="Python WEI module to control the Epoch 2 Platereader",
    model="Epoch 2",
)

# ***********#
# *Lifecycle*#
# ***********#

@epoch2_module.startup()
def custom_startup_handler(state: State):
    """
    Custom startup handler that is called whenever the module is started.

    If this isn't provided, the default startup handler will be used, which will do nothing.
    """
    state.gen5 = Gen5Interface(com_port=state.com_port)

    # state.interface = interface.initialize()  # *Initialize the device, if needed


@epoch2_module.shutdown()
def custom_shutdown_handler(state: State):
    """
    Custom shutdown handler that is called whenever the module is shutdown.

    If this isn't provided, the default shutdown handler will be used, which will do nothing.
    """

    # state.interface.disconnect()  # *Close device connection or do other cleanup, if needed


@epoch2_module.state_handler()
def custom_state_handler(state: State) -> ModuleState:
    """
    Custom state handler that is called whenever the modules state is requested via the REST API.

    If this isn't provided, the default state handler will be used, which will return the following:

    ModuleState(status=state.status, error=state.error)
    """

    # state.interface.query_state(state)  # *Query the state of the device, if supported

    return ModuleState.model_validate(
        {
            "status": state.status,  # *Required
            "error": state.error,
            # *Custom state fields
            "sum": state.sum,
            "difference": state.difference,
        }
    )


###########
# Actions #
###########

# TODO: Define functions to handle each action the device should be able to perform


@epoch2_module.action(
    name="add", description="An example action that adds two numbers together."
)
def add(
    a: Annotated[float, "First number to add"],
    b: Annotated[float, "Second number to add"],
    state: State,  # *This is an optional argument that can be used to access the current state of the module
) -> StepResponse:
    """
    Add two numbers together

    Example workflow step yaml:

    - name: Add on epoch2
      module: epoch2
      action: add
      args:
        a: 5
        b: 7
    """

    state.sum = a + b

    return StepResponse.step_succeeded(state.sum)


# * If you don't specify a name or description, the function name and docstring will be used
@epoch2_module.action()
def subtract(
    a: Annotated[float, "First number to subtract from"],
    b: Annotated[float, "Second number to subtract"],
    action: ActionRequest,  # *This is an optional argument that can be used to access the entire action request
    state: State,  # *This is an optional argument that can be used to access the current state of the module
) -> StepResponse:
    """
    Subtract two numbers

    Example workflow step yaml:

    - name: Subtract on epoch2
      module: epoch2
      action: subtract
      args:
        a: 12
        b: 10
    """

    # state.difference = a - b
    state.difference = (
        action.args["a"] - action.args["b"]
    )  # *This is equivalent to the above
    state.difference -= action.args.get(
        "c", 0
    )  # * You can also use get to provide a default value

    return StepResponse.step_succeeded(state.difference)


@epoch2_module.action(name="run_protocol", description="Run a protocol file")
def run_protocol(
    protocol: Annotated[UploadFile, "Protocol file to run"],
) -> StepFileResponse:
    """
    Run a protocol file

    Example workflow step yaml:

    - name: Run protocol on epoch2
      module: epoch2
      action: run_protocol
      files:
        protocol: path/to/protocol/file
    """
    # *Save the protocol file to a temporary location
    with NamedTemporaryFile() as f:
        f.write(protocol.file.read())
        f.seek(0)

        # *Run protocol file
        interface.run_protocol(Path(f.name))

    output_file = Path("path/to/output/file")

    return StepFileResponse(
        status=StepStatus.SUCCEEDED,
        path=output_file,
    )


# * If you don't want to/can't use the decorator, you can also add actions like this:
def print(output: str) -> StepResponse:
    """
    Print a message
    """
    print(output)
    return StepResponse.step_succeeded()


epoch2_module.actions.append(
    ModuleAction(
        name="print",
        description="A simple print action",
        function=print,
        args=[
            ModuleActionArg(name="output", type="str", description="Message to print")
        ],
    )
)


if __name__ == "__main__":
    epoch2_module.start()
