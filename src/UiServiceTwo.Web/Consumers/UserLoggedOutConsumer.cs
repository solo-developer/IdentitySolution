using IdentitySolution.Shared.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace UiServiceTwo.Web.Consumers;

public class UserLoggedOutConsumer : IConsumer<IUserLoggedOut>
{
    private readonly ILogger<UserLoggedOutConsumer> _logger;
    private readonly Services.IGlobalSessionStore _sessionStore;

    public UserLoggedOutConsumer(ILogger<UserLoggedOutConsumer> logger, Services.IGlobalSessionStore sessionStore)
    {
        _logger = logger;
        _sessionStore = sessionStore;
    }

    public Task Consume(ConsumeContext<IUserLoggedOut> context)
    {
        _logger.LogInformation("User {UserName} (ID: {UserId}) logged out globally. Invalidating local sessions in UI Two.", 
            context.Message.UserName, context.Message.UserId);
        
        _sessionStore.InvalidateUser(context.Message.UserId);
        
        return Task.CompletedTask;
    }
}
