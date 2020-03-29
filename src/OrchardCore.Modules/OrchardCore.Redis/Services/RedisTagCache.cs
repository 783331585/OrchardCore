using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Cache;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace OrchardCore.Redis.Services
{
    public class RedisTagCache : ITagCache
    {
        private readonly IRedisService _redis;
        private readonly string _prefix;
        private readonly IEnumerable<ITagRemovedEventHandler> _tagRemovedEventHandlers;
        private readonly ILogger<RedisTagCache> _logger;

        public RedisTagCache(
            IRedisService redis,
            ShellSettings shellSettings,
            IEnumerable<ITagRemovedEventHandler> tagRemovedEventHandlers,
            ILogger<RedisTagCache> logger)
        {
            _redis = redis;
            _prefix = shellSettings.Name + ":Tag:";
            _tagRemovedEventHandlers = tagRemovedEventHandlers;
            _logger = logger;
        }

        public async Task TagAsync(string key, params string[] tags)
        {
            await _redis.ConnectAsync();

            if (!_redis.IsConnected)
            {
                return;
            }

            foreach (var tag in tags)
            {
                await _redis.Database.SetAddAsync(_prefix + tag, key);
            }
        }

        public async Task<IEnumerable<string>> GetTaggedItemsAsync(string tag)
        {
            await _redis.ConnectAsync();

            if (!_redis.IsConnected)
            {
                return Enumerable.Empty<string>();
            }

            var values = await _redis.Database.SetMembersAsync(_prefix + tag);

            if (values == null || values.Length == 0)
            {
                return Enumerable.Empty<string>();
            }

            return values.Select(v => (string)v).ToArray();
        }

        public async Task RemoveTagAsync(string tag)
        {
            await _redis.ConnectAsync();

            if (!_redis.IsConnected)
            {
                return;
            }

            var values = await _redis.Database.SetMembersAsync(_prefix + tag);

            if (values == null || values.Length == 0)
            {
                return;
            }

            var set = values.Select(v => (string)v).ToArray();

            await _redis.Database.KeyDeleteAsync(_prefix + tag);

            await _tagRemovedEventHandlers.InvokeAsync(x => x.TagRemovedAsync(tag, set), _logger);
        }
    }
}
