"""Input simulation for Play mode QA testing."""

from typing import Annotated, Any, Optional

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    group="core",
    description=(
        "Input simulation for automated QA testing in Play mode. "
        "Actions: key_press, key_hold, key_release, mouse_click, mouse_move, "
        "ui_click, ui_type, ui_get_text. Works with both legacy Input Manager "
        "and new Input System via EventSystem APIs."
    ),
    annotations=ToolAnnotations(
        title="Simulate Input",
        destructiveHint=False,
        readOnlyHint=False,
    ),
)
async def simulate_input(
    ctx: Context,
    action: Annotated[str, "Action: key_press, key_hold, key_release, mouse_click, mouse_move, ui_click, ui_type, ui_get_text"],
    key: Annotated[Optional[str], "KeyCode name for keyboard actions"] = None,
    duration: Annotated[Optional[float], "Duration in seconds for key_hold"] = None,
    x: Annotated[Optional[float], "Screen X coordinate for mouse actions"] = None,
    y: Annotated[Optional[float], "Screen Y coordinate for mouse actions"] = None,
    button: Annotated[Optional[str], "Mouse button: left, right, middle"] = None,
    element_name: Annotated[Optional[str], "UI element name for ui_click, ui_type, ui_get_text"] = None,
    text: Annotated[Optional[str], "Text to type for ui_type action"] = None,
) -> dict[str, Any]:
    params_dict: dict[str, Any] = {"action": action}
    if key is not None:
        params_dict["key"] = key
    if duration is not None:
        params_dict["duration"] = duration
    if x is not None:
        params_dict["x"] = x
    if y is not None:
        params_dict["y"] = y
    if button is not None:
        params_dict["button"] = button
    if element_name is not None:
        params_dict["elementName"] = element_name
    if text is not None:
        params_dict["text"] = text

    unity_instance = await get_unity_instance_from_context(ctx)
    result = await send_with_unity_instance(
        async_send_command_with_retry, unity_instance, "simulate_input", params_dict
    )
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
