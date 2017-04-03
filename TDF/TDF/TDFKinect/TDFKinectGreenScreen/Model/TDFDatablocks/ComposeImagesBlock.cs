using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace TDFKinectGreenScreen.Model.TDFDatablocks
{
    /// <summary>
    /// The last block, combine all sources to form the composite image
    /// </summary>
    internal class ComposeImagesBlock : ISourceBlock<BitmapSource>
    {
        //Serves as the input block for receiving the background picture
        private readonly ActionBlock<BitmapSource> _imageUpdater;

        //Serves as the block that joins the depth and color camera data
        private readonly JoinBlock<DepthImageFrameInfo, ColorImageFrameInfo> _joinSources =
            new JoinBlock<DepthImageFrameInfo, ColorImageFrameInfo>(new GroupingDataflowBlockOptions { Greedy = false, BoundedCapacity = 1 });


        //Serves as the block that receive all inputs and compose them
        private readonly ActionBlock<Tuple<DepthImageFrameInfo, ColorImageFrameInfo>> _mergeImageBlock;

        //Serves as the output block of the compose image
        private readonly BroadcastBlock<BitmapSource> _resultBlock = new BroadcastBlock<BitmapSource>(i => i);

        // Indicates opaque in an opacity mask
        private const int OpaquePixelValue = -1;

        //The kinect manager, used for mapping the color and depth data
        private readonly IKinectManager _kinectManager;

        /// Reusable per thread buffer for building the playe image region
        [ThreadStatic] private static WriteableBitmap _colorBitmap;

        /// Reusable per thread buffer for building the playe depth mask
        [ThreadStatic] private static WriteableBitmap _playerOpacityMaskImage;


        /// Intermediate storage for the depth to color mapping
        private ColorImagePoint[] _colorCoordinates;

        /// Intermediate storage for the green screen opacity mask
        private int[] _greenScreenPixelData;

        //Keep the last image, the background image is updated far less then the player image
        private BitmapSource _lastImage;

        /// <summary>
        /// Build a new Compose block
        /// </summary>
        /// <param name="kinectManager">The Kinect Manager </param>
        public ComposeImagesBlock(IKinectManager kinectManager)
        {
            _kinectManager = kinectManager;
            
            //Build the internal TDF network
            _imageUpdater = new ActionBlock<BitmapSource>(image => _lastImage = image);
            _mergeImageBlock =
                new ActionBlock<Tuple<DepthImageFrameInfo, ColorImageFrameInfo>>(t => Merge(t, _lastImage),
                                                                                 new ExecutionDataflowBlockOptions
                                                                                     {BoundedCapacity = 1});
            _joinSources.LinkTo(_mergeImageBlock);

            _greenScreenPixelData = new int[0];
            _colorCoordinates = new ColorImagePoint[0];
        }

        /// <summary>
        /// Connect the depth sensor source block here
        /// </summary>
        public ITargetBlock<DepthImageFrameInfo> DepthCameraTarget
        {
            get { return _joinSources.Target1; }
        }

        /// <summary>
        /// Connect the color sensor source here
        /// </summary>
        public ITargetBlock<ColorImageFrameInfo> ColorCameraTarget
        {
            get { return _joinSources.Target2; }
        }

        /// <summary>
        /// Connect the background image source here
        /// </summary>
        public ITargetBlock<BitmapSource> ImageTarget
        {
            get { return _imageUpdater; }
        }

        #region ISourceBlock<BitmapSource> Members

        public void Complete()
        {
            _joinSources.Complete();
            _mergeImageBlock.Complete();
            _resultBlock.Complete();
        }

        void IDataflowBlock.Fault(Exception exception)
        {
            ((IDataflowBlock) _joinSources).Fault(exception);
            ((IDataflowBlock) _mergeImageBlock).Fault(exception);
            ((IDataflowBlock) _resultBlock).Fault(exception);
        }

        public Task Completion
        {
            get { return _resultBlock.Completion; }
        }

        public IDisposable LinkTo(ITargetBlock<BitmapSource> target, DataflowLinkOptions linkOptions)
        {
            return _resultBlock.LinkTo(target, linkOptions);
        }

        BitmapSource ISourceBlock<BitmapSource>.ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<BitmapSource> target,
                                           out bool messageConsumed)
        {
            return ((ISourceBlock<BitmapSource>) _resultBlock).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        bool ISourceBlock<BitmapSource>.ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<BitmapSource> target)
        {
            return ((ISourceBlock<BitmapSource>) _resultBlock).ReserveMessage(messageHeader, target);
        }

        void ISourceBlock<BitmapSource>.ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<BitmapSource> target)
        {
            ((ISourceBlock<BitmapSource>) _resultBlock).ReleaseReservation(messageHeader, target);
        }

        #endregion

        /// <summary>
        /// Do the image composition
        /// </summary>
        /// <param name="tuple">The depth and color information</param>
        /// <param name="image">The background image</param>
        private void Merge(Tuple<DepthImageFrameInfo, ColorImageFrameInfo> tuple, BitmapSource image)
        {
            //No background
            if (image == null)
                return;

            DepthImageFrameInfo depthInfo = tuple.Item1;
            ColorImageFrameInfo colorInfo = tuple.Item2;

            //Initiate buffer
            if (_greenScreenPixelData.Length != depthInfo.DepthStreamFramePixelDataLength)
                _greenScreenPixelData = new int[depthInfo.DepthStreamFramePixelDataLength];
            Array.Clear(_greenScreenPixelData, 0, _greenScreenPixelData.Length);

            //Initiate buffer
            if (_colorCoordinates.Length != depthInfo.DepthStreamFramePixelDataLength)
                _colorCoordinates = new ColorImagePoint[depthInfo.DepthStreamFramePixelDataLength];
            Array.Clear(_colorCoordinates, 0, _colorCoordinates.Length);

            _kinectManager.MapDepthFrameToColorFrame(
                depthInfo.Format,
                depthInfo.FrameData,
                colorInfo.Format,
                _colorCoordinates);

            // loop over each row and column of the depth
            for (int y = 0; y < depthInfo.Height; ++y)
                WritePlayersMask(depthInfo, y);

            WriteColorPixels(colorInfo);


            //Use the bigger image as the size for the resulting picture
            int width = Math.Max(colorInfo.Width, image.PixelWidth);
            int height = Math.Max(colorInfo.Height, image.PixelHeight);

            // create a render target that we'll render our images to
            var renderBitmap = new RenderTargetBitmap(width, height,
                                                    96.0, 96.0, PixelFormats.Pbgra32);


            var imageBrushSource = new ImageBrush
                                       {
                                           ImageSource = image,
                                           Stretch = Stretch.Fill,
                                           TileMode = TileMode.None,
                                           AlignmentX = AlignmentX.Left,
                                           AlignmentY = AlignmentY.Top,
                                           Opacity = 1,
                                       };
            var imageBrushMask = new ImageBrush
                                     {
                                         ImageSource = _playerOpacityMaskImage,
                                         Stretch = Stretch.UniformToFill,
                                         TileMode = TileMode.None,
                                         AlignmentX = AlignmentX.Left,
                                         AlignmentY = AlignmentY.Top,
                                         Opacity = 1,
                                     };

            var imageBrushPlayer = new ImageBrush
                                       {
                                           ImageSource = _colorBitmap,
                                           Stretch = Stretch.UniformToFill,
                                           TileMode = TileMode.None,
                                           AlignmentX = AlignmentX.Left,
                                           AlignmentY = AlignmentY.Top,
                                           Opacity = 1,
                                       };


            var drawingVisual = new DrawingVisual();

            //Compose images
            using (DrawingContext dc = drawingVisual.RenderOpen())
            {
                dc.DrawRectangle(imageBrushSource, null /* no pen */,
                                 new Rect(0, 0, width, height));
                dc.PushOpacityMask(imageBrushMask);
                dc.DrawRectangle(imageBrushPlayer, null /* no pen */,
                                 new Rect(0, 0, width,
                                          height));
            }
            var result = new RenderTargetBitmap(width,
                                                height,
                                                renderBitmap.DpiX, renderBitmap.DpiY,
                                                renderBitmap.Format);
            result.Render(drawingVisual);

            //Make it thread context free
            result.Freeze();

            //broadcast the result
            _resultBlock.SendAsync(result);
        }

        /// <summary>
        /// Extract the player image
        /// </summary>
        /// <param name="colorInfo">The color information</param>
        private void WriteColorPixels(ColorImageFrameInfo colorInfo)
        {
            if (_colorBitmap == null)
                _colorBitmap = new WriteableBitmap(colorInfo.Width, colorInfo.Height,
                                                   96.0, 96.0, PixelFormats.Bgr32,
                                                   null);

            // Write the color pixel data into our bitmap
            _colorBitmap.WritePixels(
                new Int32Rect(0, 0, _colorBitmap.PixelWidth, _colorBitmap.PixelHeight),
                colorInfo.FrameData,
                _colorBitmap.PixelWidth*sizeof (int), 0);
        }

        /// <summary>
        /// Extract the depth image and create a mask
        /// </summary>
        /// <param name="depthInfo">The depth info</param>
        /// <param name="y">The current line in the depth and color map</param>
        private void WritePlayersMask(DepthImageFrameInfo depthInfo, int y)
        {
            for (int x = 0; x < depthInfo.Width; ++x)
            {
                // calculate index into depth array
                int depthIndex = x + (y*depthInfo.Width);

                short depthPixel = depthInfo.FrameData[depthIndex];

                int player = depthPixel & DepthImageFrame.PlayerIndexBitmask;

                // if we're tracking a player for the current pixel, do green screen
                if (player > 0)
                {
                    // retrieve the depth to color mapping for the current depth pixel
                    ColorImagePoint colorImagePoint =
                        _colorCoordinates[depthIndex];

                    // scale color coordinates to depth resolution
                    int colorInDepthX = colorImagePoint.X/
                                        depthInfo.ColorToDepthDivisor;
                    int colorInDepthY = colorImagePoint.Y/
                                        depthInfo.ColorToDepthDivisor;

                    // make sure the depth pixel maps to a valid point in color space
                    // check y > 0 and y < depthHeight to make sure we don't write outside of the array
                    // check x > 0 instead of >= 0 since to fill gaps we set opaque current pixel plus the one to the left
                    // because of how the sensor works it is more correct to do it this way than to set to the right
                    if (colorInDepthX > 0 && colorInDepthX < depthInfo.Width &&
                        colorInDepthY >= 0 &&
                        colorInDepthY < depthInfo.Height)
                    {
                        // calculate index into the green screen pixel array
                        int greenScreenIndex = colorInDepthX +
                                               (colorInDepthY*depthInfo.Width);

                        // set opaque
                        _greenScreenPixelData[greenScreenIndex] =
                            OpaquePixelValue;

                        // compensate for depth/color not corresponding exactly by setting the pixel 
                        // to the left to opaque as well
                        _greenScreenPixelData[greenScreenIndex - 1] =
                            OpaquePixelValue;
                    }
                }
            }

            if (_playerOpacityMaskImage == null)
            {
                _playerOpacityMaskImage = new WriteableBitmap(
                    depthInfo.Width,
                    depthInfo.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null);
            }

            _playerOpacityMaskImage.WritePixels(
                new Int32Rect(0, 0, depthInfo.Width, depthInfo.Height),
                _greenScreenPixelData,
                depthInfo.Width*((_playerOpacityMaskImage.Format.BitsPerPixel + 7)/8), 0);
        }
    }
}