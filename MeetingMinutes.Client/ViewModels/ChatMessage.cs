using System.ComponentModel;

namespace MeetingMinutes.ViewModels;

public class ChatMessage : INotifyPropertyChanged
{
    private string _content;
    private bool _isStreaming = true;

    public bool IsUser { get; }

    public bool IsStreaming
    {
        get => _isStreaming;
        set
        {
            _isStreaming = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsStreaming)));
        }
    }

    public string Content
    {
        get => _content;
        set 
        { 
            _content = value; 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content))); 
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChatMessage(bool isUser, string content = "")
    {
        IsUser = isUser;
        _content = content;
    }
}
