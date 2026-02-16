using System.Windows;
using CryptoMail.ViewModels;

namespace CryptoMail;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void SenderView_Loaded(object sender, RoutedEventArgs e)
    {

    }
}
