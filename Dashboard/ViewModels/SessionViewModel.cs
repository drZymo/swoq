using Avalonia.Media;
using Swoq.InfraUI.ViewModels;

namespace Swoq.Dashboard.ViewModels;

internal class SessionViewModel(string id, string userName, int level, bool isQuest, bool isActive, bool isFinished) : ViewModelBase
{
    public string Id { get; } = id;
    public string UserName { get; } = userName;

    private int level = level;
    public int Level
    {
        get => level;
        set
        {
            if (level != value)
            {
                level = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsQuest { get; } = isQuest;

    private bool isActive = isActive;
    public bool IsActive
    {
        get => isActive;
        set
        {
            if (isActive != value)
            {
                isActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextColor));
            }
        }
    }

    private bool isFinished = isFinished;
    public bool IsFinished
    {
        get => isFinished;
        set
        {
            if (isFinished != value)
            {
                isFinished = value;
                OnPropertyChanged();
            }
        }
    }

    public IImmutableSolidColorBrush TextColor
    {
        get => IsActive ? (IsQuest ? Brushes.Gold : Brushes.White) : Brushes.Gray;
    }
}
