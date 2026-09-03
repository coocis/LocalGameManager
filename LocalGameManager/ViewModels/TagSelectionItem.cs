using LocalGameManager.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LocalGameManager.ViewModels;

public sealed class TagSelectionItem : INotifyPropertyChanged
{
    private bool _isSelected;
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TagType Type { get; init; }
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
