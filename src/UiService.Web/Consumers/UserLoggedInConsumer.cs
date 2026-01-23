
using IdentitySolution.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using UiService.Web.Hubs;

namespace UiService.Web.Consumers;

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

        // Notify ONLY the specific user's connected clients (for instant redirect if already logged in previously)
        // Note: This requires the client to be authenticated with the same UserId.
        await _hubContext.Clients.User(context.Message.UserId.ToString()).SendAsync("UserLoggedIn", context.Message.UserId);

        // Notify ALL anonymous clients to check their session silently
        // This handles cases where the user is NOT yet logged in on the client app (Anonymous)
        // but just logged in via Identity Service in another tab.
        await _hubContext.Clients.All.SendAsync("CheckSession");
    }
}
