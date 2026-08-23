namespace TruckNav.Connector.App.Views;

public partial class GpsCaptureView : UserControl
{
    public GpsCaptureView()
    {
        InitializeComponent();
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.GpsCaptureViewModel viewModel)
        {
            await viewModel.DeactivateAsync();
        }
    }
}
