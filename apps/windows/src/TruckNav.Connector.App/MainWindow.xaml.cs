using TruckNav.Connector.App.ViewModels;

namespace TruckNav.Connector.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
