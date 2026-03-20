using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CryptoMail.ViewModels;

namespace CryptoMail.Views;

public partial class ReceiverView : UserControl
{
    public ReceiverView()
    {
        InitializeComponent();
    }

    private void ReceiverPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReceiverViewModel vm && sender is PasswordBox box)
        {
            vm.Settings.Password = box.Password;
        }
    }

    private void PasswordReveal_MouseDown(object sender, MouseButtonEventArgs e)
    {
        ReceiverPasswordReveal.Text = ReceiverPasswordBox.Password;
        ReceiverPasswordBox.Visibility = Visibility.Collapsed;
        ReceiverPasswordReveal.Visibility = Visibility.Visible;
    }

    private void PasswordReveal_MouseUp(object sender, MouseButtonEventArgs e)
    {
        ReceiverPasswordReveal.Visibility = Visibility.Collapsed;
        ReceiverPasswordBox.Visibility = Visibility.Visible;
    }
}
