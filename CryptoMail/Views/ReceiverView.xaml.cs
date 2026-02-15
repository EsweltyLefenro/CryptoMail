using System.Windows.Controls;
using CryptoMail.ViewModels;

namespace CryptoMail.Views;

public partial class ReceiverView : UserControl
{
    public ReceiverView()
    {
        InitializeComponent();
    }

    private void ReceiverPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ReceiverViewModel vm && sender is PasswordBox box)
        {
            vm.Settings.Password = box.Password;
        }
    }
}
