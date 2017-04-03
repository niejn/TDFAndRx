using System.ComponentModel;
using System.Threading.Tasks.Dataflow;
using System.Windows.Media;

namespace TDFKinectGreenScreen.ViewModel
{
    /// <summary>
    /// The Main ViewModel interface
    /// </summary>
    public interface IKinectGreenScreenViewModel : INotifyPropertyChanged
    {
        ImageSource GreenScreenImage { get; set; }
        ISourceBlock<ImageSource> GreenScreenImageGenerator { set; }
        bool IsNearMode { get; set; }
    }
}