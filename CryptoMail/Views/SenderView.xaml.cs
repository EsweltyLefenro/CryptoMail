using System.Windows.Controls;
using CryptoMail.ViewModels;

namespace CryptoMail.Views;

public partial class SenderView : UserControl
{
    public SenderView()
    {
        InitializeComponent();
    }

    private void SenderPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SenderViewModel vm && sender is PasswordBox box)
        {
            vm.Settings.Password = box.Password;
        }
    }
}
