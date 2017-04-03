using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TDFKinectGreenScreen.Model.TDFDatablocks
{
    /// <summary>
    /// A block that transforms the background image to green or back
    /// </summary>
    internal class GreenTransformBlock : IPropagatorBlock<BitmapSource, BitmapSource>
    {
        //Serves as the result broadcaster
        private readonly BroadcastBlock<BitmapSource> _broadcastBlock = new BroadcastBlock<BitmapSource>(i => i);

        //receives the gesture command
        private readonly ActionBlock<BackgroundImageCommand> _targetCommand;

        //Serves as the image input block
        private readonly ActionBlock<BitmapSource> _targetImage;

        //The transition to/from green timer
        private readonly Timer _timer;

        //No, transition. Green screen
        private bool _inGreenOnlyState;

        //No transition. Image only screen
        private bool _inImageOnlyState;

        //In transition. A composite screen
        private bool _inTransition;

        //The last source image
        private BitmapSource _inputImage;

        //The last output image
        private BitmapSource _outputImage;

        //The green transperancy 
        private double _pixelGreenEffect;

        //The fade to/from green pace
        private double _step = 0.1;

        /// <summary>
        /// Create the Green transform block
        /// </summary>
        public GreenTransformBlock()
        {
            //Build the inner blocks            
            _targetImage = new ActionBlock<BitmapSource>(bitmapSource => OnNewImage(bitmapSource));
            _targetCommand = new ActionBlock<BackgroundImageCommand>(command => OnNewCommand(command));

            //Start with full image
            _inImageOnlyState = true;

            //Initiate the transition timer
            _timer = new Timer(100) {AutoReset = true, Enabled = false};
            _timer.Elapsed += Step;
        }

        /// <summary>
        /// Connect here the gesture command source block
        /// </summary>
        public ActionBlock<BackgroundImageCommand> TargetCommand
        {
            get { return _targetCommand; }
        }

        #region IPropagatorBlock<BitmapSource,BitmapSource> Members

        BitmapSource ISourceBlock<BitmapSource>.ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<BitmapSource> target,
                                           out bool messageConsumed)
        {
            return ((ISourceBlock<BitmapSource>) _broadcastBlock).ConsumeMessage(messageHeader, target,
                                                                                 out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<BitmapSource> target, DataflowLinkOptions linkOptions)
        {
            return _broadcastBlock.LinkTo(target, linkOptions);
        }

        void ISourceBlock<BitmapSource>.ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<BitmapSource> target)
        {
            ((ISourceBlock<BitmapSource>) _broadcastBlock).ReleaseReservation(messageHeader, target);
        }

        bool ISourceBlock<BitmapSource>.ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<BitmapSource> target)
        {
            return ((ISourceBlock<BitmapSource>) _broadcastBlock).ReserveMessage(messageHeader, target);
        }

        //complete inner blocks as well
        public void Complete()
        {
            _broadcastBlock.Complete();
            _targetImage.Complete();
            TargetCommand.Complete();
        }

        public Task Completion
        {
            get { return _broadcastBlock.Completion; }
        }

        void IDataflowBlock.Fault(Exception exception)
        {
            ((ISourceBlock<BitmapSource>) _broadcastBlock).Fault(exception);
        }

        DataflowMessageStatus ITargetBlock<BitmapSource>.OfferMessage(DataflowMessageHeader messageHeader, BitmapSource messageValue,
                                                  ISourceBlock<BitmapSource> source, bool consumeToAccept)
        {
            return ((ITargetBlock<BitmapSource>) _targetImage).OfferMessage(messageHeader, messageValue, source,
                                                                            consumeToAccept);
        }

        #endregion

        //transfer the image to/from green on step at a time(r)
        private void Step(object sender, ElapsedEventArgs e)
        {
            _pixelGreenEffect += _step;
            if (_pixelGreenEffect >= 1)
            {
                _pixelGreenEffect = 1;
                _inGreenOnlyState = true;
                _inImageOnlyState = false;
                FinishTransition();
            }

            if (_pixelGreenEffect <= 0)
            {
                _pixelGreenEffect = 0;
                _inImageOnlyState = true;
                _inGreenOnlyState = false;
                FinishTransition();
            }
            this.Post(null); //post an image, the same as calling OfferMessage, but simpler
        }

        private void FinishTransition()
        {
            _inTransition = false;
            RenderOutputImage();
            _timer.Stop();
        }

        /// <summary>
        /// Called when a new gesture command is received
        /// </summary>
        /// <param name="command">The green command</param>
        private void OnNewCommand(BackgroundImageCommand command)
        {
            //Check if we are already in the desire state
            if (!_inTransition &&
                (command == BackgroundImageCommand.ToGreenScreen && _inGreenOnlyState ||
                 command == BackgroundImageCommand.FromGreenScreen && _inImageOnlyState))
                return;

            //To or from green
            _step = command == BackgroundImageCommand.ToGreenScreen ? Math.Abs(_step) : -Math.Abs(_step);
            _inTransition = true;
            _timer.Enabled = true;
            _timer.Start();
        }

        /// <summary>
        /// Called when a new background image arives
        /// </summary>
        /// <param name="imageSource">The new image</param>
        private void OnNewImage(BitmapSource imageSource)
        {
            //Make sure we have an image
            _inputImage = imageSource ?? _inputImage ?? _outputImage;
            if (_inputImage == null)
                return;

            //Make sure we get the right amount of green composed with the image
            if (_inTransition)
            {
                RenderOutputImage();
                return;
            }

            //Just transfer the image and save it
            if (_inImageOnlyState)
            {
                _broadcastBlock.SendAsync(_inputImage);
                _outputImage = _inputImage;
                return;
            }
            Debug.Assert(_inGreenOnlyState);
            _broadcastBlock.SendAsync(_outputImage);
        }

        //Create the composition
        private void RenderOutputImage()
        {
            var drawVisual = new DrawingVisual();


            var imageBrushTarget = new ImageBrush
                                       {
                                           ImageSource = _inputImage,
                                           Stretch = Stretch.Fill,
                                           TileMode = TileMode.None,
                                           AlignmentX = AlignmentX.Left,
                                           AlignmentY = AlignmentY.Top,
                                           Opacity = 1 - _pixelGreenEffect,
                                       };

            var greenBrush = new SolidColorBrush
                                 {
                                     Color = Colors.GreenYellow,
                                     Opacity = _pixelGreenEffect,
                                 };

            using (DrawingContext dc = drawVisual.RenderOpen())
            {
                dc.DrawRectangle(greenBrush, null, new Rect(0, 0, _inputImage.Width, _inputImage.Height));
                dc.DrawRectangle(imageBrushTarget, null, new Rect(0, 0, _inputImage.Width, _inputImage.Height));
            }
            var result = new RenderTargetBitmap((int) _inputImage.Width, (int) _inputImage.Height, _inputImage.DpiX,
                                                _inputImage.DpiY,
                                                PixelFormats.Default);
            result.Render(drawVisual);

            //Make it thread agnostics
            result.Freeze();

            _outputImage = result;
            _broadcastBlock.SendAsync(_outputImage);
        }
    }
}