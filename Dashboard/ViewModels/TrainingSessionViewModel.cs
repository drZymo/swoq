using Avalonia.Media;
using Swoq.InfraUI.ViewModels;

namespace Swoq.Dashboard.ViewModels;

internal class TrainingSessionViewModel(string id, string userName, int level, bool isActive, bool isFinished) : ViewModelBase
{
    private bool isActive = isActive;
    private bool isFinished = isFinished;

    public string Id { get; } = id;
    public string UserName { get; } = userName;
    public int Level { get; } = level;
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

    public DateTime TimeCreated { get; } = DateTime.Now;

    public IImmutableSolidColorBrush TextColor
    {
        get => isActive ? Brushes.White : Brushes.Gray;
    }
}
