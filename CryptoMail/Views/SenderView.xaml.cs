using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CryptoMail.ViewModels;

namespace CryptoMail.Views;

public partial class SenderView : UserControl
{
    public SenderView()
    {
        InitializeComponent();
    }

    private void SenderPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SenderViewModel vm && sender is PasswordBox box)
        {
            vm.Settings.Password = box.Password;
        }
    }

    private void PasswordReveal_MouseDown(object sender, MouseButtonEventArgs e)
    {
        SenderPasswordReveal.Text = SenderPasswordBox.Password;
        SenderPasswordBox.Visibility = Visibility.Collapsed;
        SenderPasswordReveal.Visibility = Visibility.Visible;
    }

    private void PasswordReveal_MouseUp(object sender, MouseButtonEventArgs e)
    {
        SenderPasswordReveal.Visibility = Visibility.Collapsed;
        SenderPasswordBox.Visibility = Visibility.Visible;
    }
}
