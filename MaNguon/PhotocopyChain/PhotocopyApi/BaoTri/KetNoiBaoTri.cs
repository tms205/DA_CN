using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
namespace PhotocopyApi.BaoTri;
public sealed class KetNoiBaoTri
{
    private readonly ConcurrentDictionary<string, HubCallerContext> _connections = new();
    public void Add(HubCallerContext context) => _connections[context.ConnectionId] = context;
    public void Remove(string id) => _connections.TryRemove(id, out _);
    public void DisconnectAll() { foreach (var context in _connections.Values) context.Abort(); }
}
