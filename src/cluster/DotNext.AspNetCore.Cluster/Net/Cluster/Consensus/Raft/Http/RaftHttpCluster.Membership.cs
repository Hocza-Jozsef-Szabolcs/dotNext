using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using IO;
    using Membership;

    internal partial class RaftHttpCluster
    {
        private sealed class ReceivedClusterConfiguration : MemoryTransferObject, IClusterConfiguration
        {
            internal ReceivedClusterConfiguration(int length)
                : base(length)
            {
            }

            public long Fingerprint { get; init; }

            long IClusterConfiguration.Length => Content.Length;
        }

        private readonly ClusterMemberAnnouncer<HttpEndPoint>? announcer;
        private Task pollingLoopTask;

        private async Task ConfigurationPollingLoop()
        {
            await foreach (var eventInfo in ConfigurationStorage.PollChangesAsync(LifecycleToken))
            {
                if (eventInfo.IsAdded)
                {
                    var member = CreateMember(eventInfo.Id, eventInfo.Address);
                    if (await AddMemberAsync(member, LifecycleToken).ConfigureAwait(false))
                        member.IsRemote = eventInfo.Address != localNode;
                    else
                        member.Dispose();
                }
                else
                {
                    var member = await RemoveMember(eventInfo.Id, LifecycleToken).ConfigureAwait(false);
                    member?.Dispose();
                }
            }
        }

        async Task<bool> IRaftHttpCluster.AddMemberAsync(ClusterMemberId id, HttpEndPoint address, CancellationToken token)
        {
            using var member = CreateMember(id, address);
            member.IsRemote = localNode != address;
            return await AddMemberAsync(member, warmupRounds, ConfigurationStorage, static m => m.EndPoint, token).ConfigureAwait(false);
        }
    }
}