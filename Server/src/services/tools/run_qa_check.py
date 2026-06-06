"""Composite QA validation suite for post-build checks."""

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
        "Composite QA validation suite. Actions: full (all checks), quick (missing_scripts + console_errors), "
        "custom (specify checks array). Checks: missing_scripts, console_errors, profiler, screenshot. "
        "Returns unified pass/fail report."
    ),
    annotations=ToolAnnotations(
        title="Run QA Check",
        destructiveHint=False,
        readOnlyHint=True,
    ),
)
async def run_qa_check(
    ctx: Context,
    action: Annotated[Optional[str], "Check scope: full, quick, or custom"] = None,
    max_errors: Annotated[Optional[int], "Max errors to report for console_errors check"] = None,
    output_folder: Annotated[Optional[str], "Output folder for screenshots"] = None,
    checks: Annotated[Optional[str], "JSON array of check names for action=custom: missing_scripts, console_errors, profiler, screenshot"] = None,
) -> dict[str, Any]:
    params_dict: dict[str, Any] = {}
    if action is not None:
        params_dict["action"] = action
    if max_errors is not None:
        params_dict["maxErrors"] = max_errors
    if output_folder is not None:
        params_dict["outputFolder"] = output_folder
    if checks is not None:
        import json
        try:
            params_dict["checks"] = json.loads(checks)
        except (json.JSONDecodeError, TypeError):
            params_dict["checks"] = [c.strip() for c in checks.split(",") if c.strip()]

    unity_instance = await get_unity_instance_from_context(ctx)
    result = await send_with_unity_instance(
        async_send_command_with_retry, unity_instance, "run_qa_check", params_dict
    )
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
