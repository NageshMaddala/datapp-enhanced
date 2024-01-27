using System.Diagnostics;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;

[Authorize]
public class MessageHub : Hub
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IHubContext<PresenceHub> _presenceHub;
    public MessageHub(IUnitOfWork uow, IMapper mapper, IHubContext<PresenceHub> presenceHub)
    {
        _uow = uow;
        _mapper = mapper;
        _presenceHub = presenceHub;
    }

    public override async Task OnConnectedAsync()
    {
        // current logged in user, gets it from ws
        var httpContext = Context.GetHttpContext();
        // Get other user from query param        
        var otherUser = httpContext.Request.Query["user"];

        var groupName = GetGroupName(Context.User.GetUsername(), otherUser);
        // Groups is from signalR, creates unique group
        // Users is added to group
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await AddToGroup(groupName);

        var messages = await _uow.MessageRepository.GetMessageThread(Context.User.GetUsername(), otherUser);

        if (_uow.HasChanges())
        {
            await _uow.Complete();
        }

        await Clients.Group(groupName).SendAsync("ReceiveMessageThread", messages);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        await RemoveFromMessageGroup();
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(CreateMessageDto createMessageDto)
    {
        var username = Context.User.GetUsername();

        // throw exception since we don't have access to controller and others because this is ws
        if (username == createMessageDto.RecipientUsername.ToLower())
            throw new HubException("You cannot send messages to yourself");

        var sender = await _uow.UserRepository.GetUserByUsernameAsync(username);
        var recipient = await _uow.UserRepository.GetUserByUsernameAsync(createMessageDto.RecipientUsername);

        if (recipient == null)
            throw new HubException("Not found user");

        var message = new Message
        {
            Sender = sender,
            Recipient = recipient,
            SenderUsername = sender.UserName,
            RecipientUsername = recipient.UserName,
            Content = createMessageDto.Content
        };

        var groupName = GetGroupName(sender.UserName, recipient.UserName);
        var group = await _uow.MessageRepository.GetMessageGroup(groupName);

        if (group.Connections.Any(x => x.Username == recipient.UserName))
        {
            message.DateRead = DateTime.UtcNow;
        }
        else
        {
            var connections = await PresenceTracker.GetConnectionsForUser(recipient.UserName);
            if (connections != null)
            {
                await _presenceHub.Clients.Clients(connections).SendAsync("NewMessageReceived",
                    new { username = sender.UserName, knownAs = sender.KnownAs });
            }
        }

        _uow.MessageRepository.AddMessage(message);

        if (await _uow.Complete())
        {
            // var group = GetGroupName(sender.UserName, recipient.UserName);
            await Clients.Group(groupName).SendAsync("NewMessage", _mapper.Map<MessageDto>(message));
        }
    }

    private string GetGroupName(string caller, string other)
    {
        var stringCompare = string.CompareOrdinal(caller, other) < 0;
        return stringCompare ? $"{caller}-{other}" : $"{other}-{caller}";
    }

    private async Task<bool> AddToGroup(string groupName)
    {
        var group = await _uow.MessageRepository.GetMessageGroup(groupName);
        var connection = new Connection(Context.ConnectionId, Context.User.GetUsername());

        if (group == null)
        {
            group = new Group(groupName);
            _uow.MessageRepository.AddGroup(group);
        }

        group.Connections.Add(connection);
        return await _uow.Complete();
    }

    private async Task RemoveFromMessageGroup()
    {
        var connection = await _uow.MessageRepository.GetConnection(Context.ConnectionId);
        _uow.MessageRepository.RemoveConnection(connection);
        await _uow.Complete();
    }
}