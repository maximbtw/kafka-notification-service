using System.Threading.Channels;
using Confluent.Kafka;
using NotificationService.Contracts;

namespace NotificationService.Consumers;

internal class KafkaOffsetCommitter
{
    private readonly IConsumer<Ignore, NotificationMessage> _consumer;
    private readonly ILogger<NotificationConsumer> _logger;
    private readonly Channel<TopicPartitionOffset> _channel;

    public KafkaOffsetCommitter(IConsumer<Ignore, NotificationMessage> consumer, ILogger<NotificationConsumer> logger)
    {
        _consumer = consumer;
        _logger = logger;
        _channel = Channel.CreateUnbounded<TopicPartitionOffset>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
    }
    
    public async Task AddToCommit(TopicPartitionOffset commitItem, CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(commitItem, ct);
    }
    
    public void StopRead()
    {
        _channel.Writer.Complete();
    }

    public async Task StartRead(CancellationToken ct)
    {
        var partitionStates = new Dictionary<TopicPartition, PartitionCommitState>();

        await foreach (TopicPartitionOffset commitItem in _channel.Reader.ReadAllAsync(ct))
        {
            TopicPartition? tp = commitItem.TopicPartition;
            Offset offset = commitItem.Offset;

            if (!partitionStates.TryGetValue(tp, out PartitionCommitState? state))
            {
                state = new PartitionCommitState();
                
                partitionStates[tp] = state;
            }

            if (offset <= state.LastCommittedOffset)
            {
                continue;
            }

            state.PendingCommits[offset] = commitItem;
            try
            {
                CommitBatch(state);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to commit batch: {ex.Message}");
                break; 
            }
        }
    }
    
    private void CommitBatch(PartitionCommitState state)
    {
        var offsetToCommit = new List<TopicPartitionOffset>();
        while (state.PendingCommits.Count > 0)
        {
            Offset nextOffset = state.LastCommittedOffset + 1;
            if (!state.PendingCommits.TryGetValue(nextOffset, out TopicPartitionOffset? nextCommitItem))
            {
                break;    
            }
            offsetToCommit.Add(nextCommitItem);
        }

        if (offsetToCommit.Count == 0)
        {
            return;
        }

        _consumer.Commit(offsetToCommit);
        state.LastCommittedOffset += offsetToCommit.Count;

        foreach (TopicPartitionOffset offset in offsetToCommit)
        {
            state.PendingCommits.Remove(offset.Offset);
        }
    }
    
    private class PartitionCommitState
    {
        public Offset LastCommittedOffset { get; set; } = Offset.Beginning;
        
        public SortedDictionary<Offset, TopicPartitionOffset> PendingCommits { get; } = new();
    }
}