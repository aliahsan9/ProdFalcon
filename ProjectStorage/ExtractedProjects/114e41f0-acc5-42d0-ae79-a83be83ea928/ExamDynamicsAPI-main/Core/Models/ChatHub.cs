using Microsoft.AspNetCore.SignalR;

namespace ExamDynamicsAPI.Core.Models{
public class ChatHub : Hub
{
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}
}