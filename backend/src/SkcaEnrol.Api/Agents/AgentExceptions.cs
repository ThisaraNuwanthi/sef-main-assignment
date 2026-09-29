namespace SkcaEnrol.Api.Agents;

// How the orchestrator reacts depends on the exception type:
//  - AgentOutputException / LlmUnavailableException / ToolFailedException -> retry the step (max 2 retries)
//  - NonRetryableAgentException subclasses -> stop at once and fail the workflow safely

/// <summary>An LLM answer that is not valid JSON or breaks the output contract. Retrying may help.</summary>
public class AgentOutputException(string message) : Exception(message);

/// <summary>The LLM provider could not be reached or answered with an error. Retrying may help.</summary>
public class LlmUnavailableException(string message) : Exception(message);

/// <summary>An external tool (e.g. Lichess) failed.</summary>
public class ToolFailedException(string message) : Exception(message);

/// <summary>Retrying the same step cannot fix this.</summary>
public abstract class NonRetryableAgentException(string message) : Exception(message);

/// <summary>An agent tried to use a tool outside its allow-list (least privilege breach).</summary>
public class AgentToolNotAllowedException(string agent, string tool)
    : NonRetryableAgentException($"{agent} is not allowed to use the tool '{tool}'.");

/// <summary>A business reason to stop (e.g. no suitable class). Recorded as the failure reason.</summary>
public class WorkflowFailedException(string message) : NonRetryableAgentException(message);
