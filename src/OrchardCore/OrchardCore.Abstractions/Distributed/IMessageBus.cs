using System;
using System.Threading.Tasks;

namespace OrchardCore.Distributed
{
    public interface IMessageBus
    {
        Task SubscribeAsync(string channel, Action<string, string> handler);
        Task PublishAsync(string channel, string message);
    }
}
