
using IdentitySolution.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using UiServiceTwo.Web.Hubs;

namespace UiServiceTwo.Web.Consumers;

public class UserLoggedInConsumer : IConsumer<IUserLoggedIn>
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<UserLoggedInConsumer> _logger;

    public UserLoggedInConsumer(IHubContext<NotificationHub> hubContext, ILogger<UserLoggedInConsumer> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IUserLoggedIn> context)
    {
        _logger.LogInformation("User Logged In Event received for User: {UserName}", context.Message.UserName);

        // Notify ONLY the specific user's connected clients
        await _hubContext.Clients.User(context.Message.UserId.ToString()).SendAsync("UserLoggedIn", context.Message.UserId);

        // Notify All to Check Session
        await _hubContext.Clients.All.SendAsync("CheckSession");
    }
}
