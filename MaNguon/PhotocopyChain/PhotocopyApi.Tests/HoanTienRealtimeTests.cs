using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PhotocopyApi.Controllers;
using PhotocopyApi.Hubs;
using PhotocopyShared.Constants;
using Xunit;

namespace PhotocopyApi.Tests;
public class HoanTienRealtimeTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RefundSignalTargetsBranchAndCeo_WithoutBankData_ConnectionFailureDoesNotUndoSavedOperation(bool disconnected)
    {
        var events=new List<(string Group,string Event,string Body)>();
        var hub=new Hub(events,disconnected);
        using var cache=new MemoryCache(new MemoryCacheOptions());
        var controller=new DonHangController(new ConfigurationBuilder().Build(),cache,hub) {
            ControllerContext=new() { HttpContext=new DefaultHttpContext() } };
        var method=typeof(DonHangController).GetMethod("BaoHoanTienDoiAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
        await (Task)method.Invoke(controller,new object[]{57,1})!;
        if(disconnected)return;
        Assert.Equal(new[]{"chi-nhanh-1","toan-chuoi"},events.Select(x=>x.Group));
        Assert.All(events,x=> {
            Assert.Equal(HubEvents.RefundChanged,x.Event);
            using var body=JsonDocument.Parse(x.Body);
            Assert.Equal(57,body.RootElement.GetProperty("MaDonHang").GetInt32());
            Assert.Equal(2,body.RootElement.EnumerateObject().Count());
        });
    }
    private sealed class Hub(List<(string,string,string)> events,bool disconnected):IHubContext<CapNhatHub> {
        public IHubClients Clients {get;}=new FakeClients(events,disconnected);
        public IGroupManager Groups=>throw new NotSupportedException();
    }
    private sealed class Proxy(string group,List<(string,string,string)> events,bool disconnected):IClientProxy {
        public Task SendCoreAsync(string method,object[] args,CancellationToken token=default) {
            if(disconnected)throw new IOException("fixture disconnected");
            events.Add((group,method,JsonSerializer.Serialize(args.Single())));return Task.CompletedTask;
        }
    }
    private sealed class FakeClients(List<(string,string,string)> events,bool disconnected):IHubClients {
        public IClientProxy Group(string name)=>new Proxy(name,events,disconnected);
        public IClientProxy All=>throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> ids)=>throw new NotSupportedException();
        public IClientProxy Client(string id)=>throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> ids)=>throw new NotSupportedException();
        public IClientProxy GroupExcept(string name,IReadOnlyList<string> ids)=>throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> names)=>throw new NotSupportedException();
        public IClientProxy User(string id)=>throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> ids)=>throw new NotSupportedException();
    }
}
