"""Play mode lifecycle management for automated QA testing."""

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
        "Play mode control for automated QA. Actions: enter, exit, pause, resume, get_state. "
        "Enter/exit support async readiness polling with configurable timeout."
    ),
    annotations=ToolAnnotations(
        title="Manage Play Mode",
        destructiveHint=False,
        readOnlyHint=False,
    ),
)
async def manage_play_mode(
    ctx: Context,
    action: Annotated[str, "Action: enter, exit, pause, resume, get_state"],
    timeout: Annotated[Optional[float], "Timeout in seconds for enter/exit operations"] = None,
) -> dict[str, Any]:
    params_dict: dict[str, Any] = {"action": action}
    if timeout is not None:
        params_dict["timeout"] = timeout

    unity_instance = await get_unity_instance_from_context(ctx)
    result = await send_with_unity_instance(
        async_send_command_with_retry, unity_instance, "manage_play_mode", params_dict
    )
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
