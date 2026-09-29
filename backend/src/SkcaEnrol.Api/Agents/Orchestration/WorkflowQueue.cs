using System.Threading.Channels;

namespace SkcaEnrol.Api.Agents.Orchestration;

/// <summary>
/// In-memory queue of workflow ids (a singleton). The HTTP request writes an id
/// and returns immediately; WorkflowWorker reads ids and runs them in the background.
/// If the app restarts, WorkflowWorker re-queues anything still marked Queued in the DB,
/// so the database, not this queue, is the source of truth.
/// </summary>
public class WorkflowQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public ChannelReader<int> Reader => _channel.Reader;

    public void Enqueue(int workflowId) => _channel.Writer.TryWrite(workflowId);
}
