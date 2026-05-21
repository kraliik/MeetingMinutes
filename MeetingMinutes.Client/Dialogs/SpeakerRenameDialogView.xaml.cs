using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace MeetingMinutes.Dialogs;

public class SpeakerRenameRow : INotifyPropertyChanged
{
    public string OriginalLabel { get; }

    private string _newName = string.Empty;
    public string NewName
    {
        get => _newName;
        set
        {
            if (_newName == value) return;
            _newName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewName)));
        }
    }

    public SpeakerRenameRow(string label)
    {
        OriginalLabel = label;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class SpeakerRenameDialogView : UserControl
{
    public ObservableCollection<SpeakerRenameRow> Rows { get; } = new();

    public SpeakerRenameDialogView(IEnumerable<string> originalLabels)
    {
        InitializeComponent();
        DataContext = this;
        foreach (var label in originalLabels)
            Rows.Add(new SpeakerRenameRow(label));
    }

    private void OkButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dict = new Dictionary<string, string>();
        foreach (var row in Rows)
        {
            if (!string.IsNullOrWhiteSpace(row.NewName))
                dict[row.OriginalLabel] = row.NewName;
        }
        DialogHost.CloseDialogCommand.Execute(dict, this);
    }

    private void SkipButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogHost.CloseDialogCommand.Execute(null, this);
    }
}
