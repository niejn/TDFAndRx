using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TDFKinectGreenScreen.Model.TDFDatablocks
{
    /// <summary>
    /// Receives gesture commands, rotates and pushes loaded pictures
    /// </summary>
    /// <remarks>Take the pictures from the Public picture folder</remarks>
    internal class BackgroundPictureManagerBlock : IPropagatorBlock<BackgroundImageCommand, BitmapSource>
    {
        #region Inner class

        /// <summary>
        /// Inner class - handles the bacground image transition
        /// </summary>
        /// <remarks>This class may be replaced with other transition effect</remarks>
        private class ImageTransition
        {
            private const int Pace = 3; //Transition step
            private readonly BackgroundPictureManagerBlock _backgroundPictureManagerBlock; //The outer instance
            private readonly Timer _timer; //Transition effect timer
            private BackgroundImageCommand _currentTransitionCommand; //The current execution transition
            private double _dx; // The step + direction of the transition
            private double _imageBorderLocation; //The current location of the x cordinate of the replaced picture (range is from -width to zero, or from zero to width)
            private bool _inTransition; 
            private BackgroundImageCommand _originalTransitionCommand; //The start transition command, it may be replaced in the middle.
            private BitmapImage _sourceImage; //The original image
            private BitmapImage _targetImage; //The new image

            /// <summary>
            /// Initiate the transition manipulation instance
            /// </summary>
            /// <param name="backgroundPictureManagerBlock">The real block</param>
            public ImageTransition(BackgroundPictureManagerBlock backgroundPictureManagerBlock)
            {
                _backgroundPictureManagerBlock = backgroundPictureManagerBlock;
                _timer = new Timer(1000/30) {AutoReset = true, Enabled = false}; // 30 times a second
                _timer.Elapsed += (s, e) => Step();
            }

            /// <summary>
            /// Start transition
            /// </summary>
            /// <param name="cmd">The transition direction</param>
            public void Move(BackgroundImageCommand cmd)
            {
                if (!_inTransition)
                {
                    _sourceImage = _backgroundPictureManagerBlock.Picture;
                    _targetImage = cmd == BackgroundImageCommand.NextImage
                                       ? _backgroundPictureManagerBlock.NextPicture
                                       : _backgroundPictureManagerBlock.PreviousPicture;
                    _imageBorderLocation = 0;
                    _originalTransitionCommand = _currentTransitionCommand = cmd;
                    _dx = cmd == BackgroundImageCommand.NextImage ? -Pace : Pace;
                    _inTransition = true;
                    _timer.Enabled = true;
                    _timer.Start();
                }
                else
                {
                    lock (this)
                    {
                        if (cmd == _currentTransitionCommand)
                        {
                            _dx *= 1.8;
                        }
                        else
                        {
                            _dx = -Math.Sign(_dx)*Pace;
                        }
                        _currentTransitionCommand = cmd;
                    }
                }
            }

            /// <summary>
            /// Do one step, called by a timer
            /// </summary>
            private void Step()
            {
                lock (this)
                {
                    if (!_inTransition)
                        return;

                    double width = Math.Max(_sourceImage.Width, _targetImage.Width);
                    double height = Math.Max(_sourceImage.Height, _targetImage.Height);

                    double accelaration = Math.Max(20 - Math.Abs(Math.Abs(_imageBorderLocation) - width/2)/40, 2);

                    _imageBorderLocation += _dx*accelaration + _dx;


                    var drawVisual = new DrawingVisual();
                    var imageBrushSource = new ImageBrush
                                               {
                                                   ImageSource = _sourceImage,
                                                   Stretch = Stretch.Fill,
                                                   TileMode = TileMode.None,
                                                   AlignmentX = AlignmentX.Left,
                                                   AlignmentY = AlignmentY.Top,
                                                   Opacity = 1,
                                                   Transform = new TranslateTransform(_imageBorderLocation, 0)
                                               };
                    imageBrushSource.Freeze();
                    var imageBrushTarget = new ImageBrush
                                               {
                                                   ImageSource = _targetImage,
                                                   Stretch = Stretch.Fill,
                                                   TileMode = TileMode.None,
                                                   AlignmentX = AlignmentX.Left,
                                                   AlignmentY = AlignmentY.Top,
                                                   Opacity = 1,
                                                   Transform =
                                                       new TranslateTransform(
                                                       _originalTransitionCommand == BackgroundImageCommand.NextImage
                                                           ? _imageBorderLocation + _targetImage.Width
                                                           : _imageBorderLocation - _targetImage.Width, 0)
                                               };
                    imageBrushTarget.Freeze();

                    using (DrawingContext dc = drawVisual.RenderOpen())
                    {
                        dc.DrawRectangle(imageBrushSource, null, new Rect(0, 0, width, height));
                        dc.DrawRectangle(imageBrushTarget, null, new Rect(0, 0, width, height));
                    }
                    var result = new RenderTargetBitmap((int) width, (int) height, _targetImage.DpiX, _targetImage.DpiY,
                                                        PixelFormats.Default);
                    result.Render(drawVisual);
                    result.Freeze();
                    _backgroundPictureManagerBlock._broadcast.SendAsync(result);

                    //end of transition
                    if (((_currentTransitionCommand == _originalTransitionCommand) && ((_dx > 0 && _imageBorderLocation >= _sourceImage.Width) ||
                        ((_dx < 0 && _imageBorderLocation <= -_sourceImage.Width)))) ||
                       ((_currentTransitionCommand != _originalTransitionCommand) && ((_dx > 0 && _imageBorderLocation >= 0) ||
                        ((_dx < 0 && _imageBorderLocation <= 0))))) 
                    {
                        _inTransition = false;
                        _timer.Enabled = false;

                        //If we finished the original transition, we need to rotate the picture
                        if (_currentTransitionCommand == _originalTransitionCommand)
                        {
                            _backgroundPictureManagerBlock.RotatePictures(_originalTransitionCommand ==
                                                                          BackgroundImageCommand.NextImage);
                        }
                    }
                }
            }
        }

        #endregion Inner class

        //Receive the gesture command, serves as the target block
        private readonly ActionBlock<BackgroundImageCommand> _actionBlock; 

        //send the result image, serves as the source block
        private readonly BroadcastBlock<BitmapSource> _broadcast = new BroadcastBlock<BitmapSource>(i => i); //brodcast the 

        //The image transition effect instance
        private readonly ImageTransition _imageTransition;


        // The paths of the picture files.
        private readonly string[] _picturePaths = CreatePicturePaths();

        //The current image index
        private int _index;

        /// <summary>
        /// Build a new image rotating block
        /// </summary>
        public BackgroundPictureManagerBlock()
        {
            _imageTransition = new ImageTransition(this);
            LoadPictures();
            _actionBlock = new ActionBlock<BackgroundImageCommand>(cmd => SetNextBackgrounImage(cmd));
            //Set first picture
            _broadcast.SendAsync(Picture);
        }


        /// <summary>
        /// Gets the previous image displayed.
        /// </summary>
        private BitmapImage PreviousPicture { get; set; }

        /// <summary>
        /// Gets the current image to be displayed.
        /// </summary>
        private BitmapImage Picture { get; set; }

        /// <summary>
        /// Gets teh next image displayed.
        /// </summary>
        private BitmapImage NextPicture { get; set; }

        #region IPropagatorBlock<BackgroundImageCommand,BitmapSource> Members

        //Delegate TDF calls to the inner blocks

        BitmapSource ISourceBlock<BitmapSource>.ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<BitmapSource> target,
                                           out bool messageConsumed)
        {
            return ((ISourceBlock<BitmapSource>) _broadcast).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<BitmapSource> target, DataflowLinkOptions linkOptions)
        {
            return _broadcast.LinkTo(target, linkOptions);
        }

        void ISourceBlock<BitmapSource>.ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<BitmapSource> target)
        {
            ((ISourceBlock<BitmapSource>) _broadcast).ReleaseReservation(messageHeader, target);
        }

        bool ISourceBlock<BitmapSource>.ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<BitmapSource> target)
        {
            return ((ISourceBlock<BitmapSource>) _broadcast).ReserveMessage(messageHeader, target);
        }

        public void Complete()
        {
            _broadcast.Complete();
        }

        public Task Completion
        {
            get { return _broadcast.Completion; }
        }

        void IDataflowBlock.Fault(Exception exception)
        {
            ((ISourceBlock<BitmapSource>)_broadcast).Fault(exception);
        }

        DataflowMessageStatus ITargetBlock<BackgroundImageCommand>.OfferMessage(DataflowMessageHeader messageHeader,
                                                  BackgroundImageCommand messageValue,
                                                  ISourceBlock<BackgroundImageCommand> source, bool consumeToAccept)
        {
            if (messageValue != BackgroundImageCommand.NextImage && messageValue != BackgroundImageCommand.PreviousImage)
                return DataflowMessageStatus.Declined;

            return ((ITargetBlock<BackgroundImageCommand>) _actionBlock).OfferMessage(messageHeader, messageValue,
                                                                                      source, consumeToAccept);
        }

        #endregion

        /// <summary>
        /// Rotate pictures and load new one
        /// </summary>
        /// <param name="isRight">Rotate direction</param>
        private void RotatePictures(bool isRight)
        {
            if (isRight)
            {
                _index++;
                PreviousPicture = Picture;
                Picture = NextPicture;
                NextPicture = LoadPicture(_index + 1);
            }
            else
            {
                _index--;
                NextPicture = Picture;
                Picture = PreviousPicture;
                PreviousPicture = LoadPicture(_index - 1);
            }
        }

        /// <summary>
        /// Start transition
        /// </summary>
        /// <param name="cmd">Transition direction</param>
        private void SetNextBackgrounImage(BackgroundImageCommand cmd)
        {
            Debug.Assert(cmd == BackgroundImageCommand.NextImage || cmd == BackgroundImageCommand.PreviousImage);

            _imageTransition.Move(cmd);
        }

        /// <summary>
        /// Load 3 pictures from the Public Picture folder
        /// </summary>
        private void LoadPictures()
        {
            PreviousPicture = LoadPicture(_index - 1);
            Picture = LoadPicture(_index);
            NextPicture = LoadPicture(_index + 1);
        }

        /// <summary>
        /// Load the picture with the given index.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <returns>Corresponding image.</returns>
        private BitmapImage LoadPicture(int index)
        {
            BitmapImage value;

            if (_picturePaths.Length != 0)
            {
                int actualIndex = index%_picturePaths.Length;
                if (actualIndex < 0)
                {
                    actualIndex += _picturePaths.Length;
                }

                Debug.Assert(0 <= actualIndex, "Index used will be non-negative");
                Debug.Assert(actualIndex < _picturePaths.Length, "Index is within bounds of path array");

                try
                {
                    value = new BitmapImage(new Uri(_picturePaths[actualIndex]));
                    value.Freeze();
                }
                catch (NotSupportedException)
                {
                    value = null;
                }
            }
            else
            {
                value = null;
            }

            return value;
        }

        /// <summary>
        /// Get list of files to display as pictures.
        /// </summary>
        /// <returns>Paths to pictures.</returns>
        private static string[] CreatePicturePaths()
        {
            var list = new List<string>();

            string commonPicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures);
            list.AddRange(Directory.GetFiles(commonPicturesPath, "*.jpg", SearchOption.AllDirectories));
            if (list.Count == 0)
            {
                list.AddRange(Directory.GetFiles(commonPicturesPath, "*.png", SearchOption.AllDirectories));
            }

            if (list.Count == 0)
            {
                string myPicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                list.AddRange(Directory.GetFiles(myPicturesPath, "*.jpg", SearchOption.AllDirectories));
                if (list.Count == 0)
                {
                    list.AddRange(Directory.GetFiles(commonPicturesPath, "*.png", SearchOption.AllDirectories));
                }
            }

            return list.ToArray();
        }
    }
}