using System.Windows;
using TDFKinectGreenScreen.ViewModel;

namespace TDFKinectGreenScreen.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = ((App) Application.Current).Container.GetExportedValue<IKinectGreenScreenViewModel>();
        }
    }
}