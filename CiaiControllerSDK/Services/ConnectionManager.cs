using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CiaiControllerSDK.Core;
using CiaiControllerSDK.Interfaces;

namespace CiaiControllerSDK.Services
{
    /// <summary>管理命名连接、共享资源组、重试和生命周期。</summary>
    public sealed class ConnectionManager : IAsyncDisposable
    {
        private sealed class Entry
        {
            public ConnectionConfiguration Configuration { get; init; }
            public ICommunication Communication { get; init; }
            public SemaphoreSlim Gate { get; init; }
        }

        private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SemaphoreSlim> _groups = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public ConnectionManager(IEnumerable<ConnectionConfiguration> configurations)
        {
            var list = (configurations ?? throw new ArgumentNullException(nameof(configurations))).ToList();
            if (list.Count == 0) throw new ArgumentException("至少需要一个连接配置", nameof(configurations));
            foreach (var c in list)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) throw new ArgumentException("连接名称不能为空");
                if (_entries.ContainsKey(c.Name)) throw new ArgumentException($"连接名称重复: {c.Name}");
                var groupName = string.IsNullOrWhiteSpace(c.ResourceGroup) ? $"@{c.Name}" : c.ResourceGroup.Trim();
                if (!_groups.TryGetValue(groupName, out var gate))
                {
                    gate = new SemaphoreSlim(c.EffectiveMaxConcurrency, c.EffectiveMaxConcurrency);
                    _groups[groupName] = gate;
                }
                _entries.Add(c.Name, new Entry
                {
                    Configuration = c,
                    Communication = CommunicationProviderRegistry.Create(c),
                    Gate = gate
                });
            }
            DefaultName = list.FirstOrDefault(c => c.IsDefault)?.Name ?? list[0].Name;
        }

        public string DefaultName { get; }
        public bool IsConnected => _entries.Values.Where(e => e.Configuration.Required && e.Configuration.ConnectOnStart)
            .All(e => e.Communication.IsConnected);
        public ICommunication Default => Get(DefaultName);
        public IReadOnlyCollection<string> Names => _entries.Keys.ToArray();

        public ICommunication Get(string name = null)
        {
            ThrowIfDisposed();
            name ??= DefaultName;
            if (!_entries.TryGetValue(name, out var entry))
                throw new KeyNotFoundException($"未配置连接: {name}");
            return entry.Communication;
        }

        public async Task<bool> ConnectAsync()
        {
            var connected = new List<Entry>();
            foreach (var entry in _entries.Values.Where(e => e.Configuration.ConnectOnStart))
            {
                if (await entry.Communication.ConnectAsync()) { connected.Add(entry); continue; }
                if (!entry.Configuration.Required) continue;
                foreach (var item in connected.AsEnumerable().Reverse()) await item.Communication.DisconnectAsync();
                return false;
            }
            return true;
        }

        public async Task<T> ExecuteAsync<T>(string name, Func<ICommunication, Task<T>> action,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            name ??= DefaultName;
            if (!_entries.TryGetValue(name, out var entry)) throw new KeyNotFoundException($"未配置连接: {name}");
            if (!await entry.Gate.WaitAsync(entry.Configuration.ResourceWaitTimeoutMs, cancellationToken))
                throw new TimeoutException($"等待连接资源超时: {name}");
            try
            {
                if (!entry.Communication.IsConnected && !await entry.Communication.ConnectAsync())
                    throw new InvalidOperationException($"连接失败: {name}");
                Exception last = null;
                var delay = entry.Configuration.RetryDelayMs;
                for (var attempt = 0; attempt <= entry.Configuration.RetryCount; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try { return await action(entry.Communication); }
                    catch (Exception ex) when (attempt < entry.Configuration.RetryCount)
                    {
                        last = ex;
                        try
                        {
                            await entry.Communication.DisconnectAsync();
                            await entry.Communication.ConnectAsync();
                        }
                        catch { }
                        if (delay > 0) await Task.Delay(delay, cancellationToken);
                        delay = (int)Math.Min(int.MaxValue, delay * entry.Configuration.RetryBackoff);
                    }
                }
                throw last ?? new InvalidOperationException("连接调用失败");
            }
            finally { entry.Gate.Release(); }
        }

        public Task ExecuteAsync(string name, Func<ICommunication, Task> action,
            CancellationToken cancellationToken = default) =>
            ExecuteAsync(name, async c => { await action(c); return true; }, cancellationToken);

        public async Task DisconnectAsync()
        {
            foreach (var entry in _entries.Values.Reverse())
                await entry.Communication.DisconnectAsync();
        }

        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ConnectionManager)); }
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            await DisconnectAsync();
            foreach (var entry in _entries.Values)
            {
                if (entry.Communication is IAsyncDisposable ad) await ad.DisposeAsync();
                else if (entry.Communication is IDisposable d) d.Dispose();
            }
            foreach (var gate in _groups.Values) gate.Dispose();
            _disposed = true;
        }
    }
}
