using System.Windows;
using System.Windows.Input;

namespace CsxPad.Wpf.Dialogs;

public partial class ConfirmDeleteDialog : Window
{
    public ConfirmDeleteDialog(string itemName, bool isFolder)
    {
        ItemName = itemName;
        WarningText = isFolder
            ? "The folder and all files below it will be permanently deleted."
            : "The file will be permanently deleted.";
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (_, _) => DialogResult = false));
    }

    public string ItemName { get; }

    public string WarningText { get; }

    private void Delete_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
