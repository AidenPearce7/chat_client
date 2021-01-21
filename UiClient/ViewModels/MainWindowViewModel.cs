using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using ReactiveUI;

namespace UiClient.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly HubConnection _connection;
        private ObservableCollection<string> _displayedMessages;
        private readonly ObservableCollection<string> _rooms;
        private readonly Dictionary<string, ObservableCollection<string>> _messages;
        private string _currentRoom;

        public string CurrentRoom
        {
            get => _currentRoom;
            set
            {
                _currentRoom = value;
                ChangeRoom();
            }
        }

        private string _message;

        public ObservableCollection<string> DisplayedMessages
        {
            get => _displayedMessages;
            set => this.RaiseAndSetIfChanged(ref _displayedMessages, value);
        }

        public ObservableCollection<string> Rooms => _rooms;
        private bool _connectionState;

        public bool ConnectionActive
        {
            get => _connectionState;
            private set => this.RaiseAndSetIfChanged(ref _connectionState, value);
        }

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }
        private string _title;
        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        public MainWindowViewModel()
        {
            _messages = new Dictionary<string, ObservableCollection<string>>();
            _rooms = new ObservableCollection<string>();
            for (var i = 1; i < 5; i++)
            {
                _rooms.Add($"Room {i}");
                _messages.Add($"Room {i}", new ObservableCollection<string>());
            }

            DisplayedMessages = _messages["Room 1"];
            _connection = new HubConnectionBuilder().WithUrl("http://localhost:5000/chathub").WithAutomaticReconnect()
                .Build();
            _connection.On<string, string, string>("ReceiveMessage",
                (room, user, msg) => { _messages[room].Add($"{user}: {msg}"); });
                _connection.On<string>("GroupLeft", (room) => { Rooms.Remove(room); });
                _connection.On<string>("UpdateGroups", (room) =>
                {
                    Rooms.Add(room);
                    _messages.Add(room,new ObservableCollection<string>());
                });
                _connection.On("UserDoesntExistError",
                    () => { DisplayedMessages.Add("[SERVER]: User doesn't exist"); });
                _connection.On<string>("UsernameChanged", (username) =>
                {
                    DisplayedMessages.Add("[SERVER]: Username Changed");
                    Title = $"{username}'s connection";
                });
                _connection.On("UsernameExistsError", () => { DisplayedMessages.Add("[SERVER]: Username taken"); });
                _connection.On("SameUserError",
                    () => { DisplayedMessages.Add("[SERVER]: Cannot open dm to the same user"); });
                _connection.On("CannotLeaveError",
                    () => { DisplayedMessages.Add("[SERVER]: Cannot leave base rooms"); });
                _connection.Reconnected += s =>
                {
                    ConnectionActive = true;
                    return Task.CompletedTask;
                };
                _connection.Closed += s =>
                {
                    ConnectionActive = false;
                    return Task.CompletedTask;
                };
                _connection.Reconnecting += s =>
                {
                    ConnectionActive = false;
                    return Task.CompletedTask;
                };

                _connection.StartAsync();
                ConnectionActive = true;
                Title = $"Username is not set";

        }

            private void ChangeRoom()
            {
                DisplayedMessages = _messages[CurrentRoom];
            }

            public async void SendMessage()
            {
                string message = Message;
                Message = string.Empty;
                if (!message.StartsWith('!'))
                {
                    await _connection.SendAsync("SendMessage", CurrentRoom, message);
                }
                else
                {
                    if (message.StartsWith("!username ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (message.Split(' ')[1].Length<1) DisplayedMessages.Add("[Client]: syntax error: no username set");
                        else await _connection.SendAsync("ChangeUsername", message.Split(" ")[1]);
                    }
                    else if (message.StartsWith("!dm ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (message.Split(' ')[1].Length<1) DisplayedMessages.Add("[Client]: syntax error: no user set");
                        else await _connection.SendAsync("OpenDirectMessage", message.Split(' ')[1]);
                    }
                    else if (message.StartsWith("!leave", StringComparison.OrdinalIgnoreCase))
                    {
                        await _connection.SendAsync("Leave", CurrentRoom);
                    }
                    else if (message.StartsWith("!status", StringComparison.OrdinalIgnoreCase))
                    {
                        DisplayedMessages.Add($"[Client]: STATUS: {_connection.State.ToString()}");
                    }
                    else if (message.StartsWith("!clear", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var room in _messages)
                        {
                            room.Value.Clear();   
                        }
                    }
                    else if (message.StartsWith("!help",StringComparison.OrdinalIgnoreCase))
                    {
                        DisplayedMessages.Add("[Client]: Available commands: !help, !dm [user], !clear, !status, !username [username], !leave");
                    }
                    else
                    {
                        DisplayedMessages.Add("[Client]: type !help to learn about commands,");
                    }

                }

            }
        }
    }
