using System.ComponentModel;
using System.Threading.Tasks.Dataflow;
using System.Windows.Media;
using TDFKinectGreenScreen.Model;

namespace TDFKinectGreenScreen.ViewModel
{
    /// <summary>
    /// The main view model
    /// </summary>
    internal class KinectGreenScreenViewModel : IKinectGreenScreenViewModel
    {
        //Gets called anytime a new image is ready
        private readonly ActionBlock<ImageSource> _newImageBlock;

        //The presented image
        private ImageSource _image;

        public KinectGreenScreenViewModel()
        {
            _newImageBlock = new ActionBlock<ImageSource>(image => GreenScreenImage = image);
        }


        public IKinectManager KinectManager { get; set; }

        #region IKinectGreenScreenViewModel Members

        /// <summary>
        /// Link the network last block to the ViewModel image receiving action block. 
        /// </summary>
        public ISourceBlock<ImageSource> GreenScreenImageGenerator
        {
            set { value.LinkTo(_newImageBlock); }
        }

        /// <summary>
        /// For databinding of the image
        /// </summary>
        public ImageSource GreenScreenImage
        {
            get { return _image; }
            set
            {
                _image = value;
                OnPropertyChanged("GreenScreenImage");
            }
        }

        public bool IsNearMode
        {
            get { return KinectManager.IsNearMode; }
            set { KinectManager.IsNearMode = value; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}