using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using MeetingMinutes.Helpers;
using MeetingMinutes.Settings;

namespace MeetingMinutes.Dialogs;

public partial class SettingsDialogView : UserControl
{
    private readonly UserSettingsData _current;

    public SettingsDialogView(UserSettingsData current)
    {
        InitializeComponent();
        _current = current;
        WpfHelpers.SelectComboBoxItem(ModelComboBox, current.OllamaModel);
    }

    private void SaveButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var result = _current.Clone();
        result.OllamaModel = (string)((ComboBoxItem)ModelComboBox.SelectedItem).Content;
        DialogHost.CloseDialogCommand.Execute(result, this);
    }
}
