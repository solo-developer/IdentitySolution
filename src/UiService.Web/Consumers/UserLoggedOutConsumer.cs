using IdentitySolution.Shared.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace UiService.Web.Consumers;

public class UserLoggedOutConsumer : IConsumer<IUserLoggedOut>
{
    private readonly ILogger<UserLoggedOutConsumer> _logger;

    public UserLoggedOutConsumer(ILogger<UserLoggedOutConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<IUserLoggedOut> context)
    {
        _logger.LogInformation("User {UserName} (ID: {UserId}) logged out. Cleaning up local session if necessary.", 
            context.Message.UserName, context.Message.UserId);
        
        // In a real application, you might invalidate caches, notify connected clients via SignalR, etc.
        
        return Task.CompletedTask;
    }
}
