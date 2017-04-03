using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Kinect;

namespace TDFKinectGreenScreen.Model
{
    /// <summary>
    /// Wrap the Kinect sensor, provide the interface to receive and manipulate Kinect data
    /// </summary>
    public class KinectManager : IKinectManager
    {
        /// <summary>
        /// Format we will use for the depth stream
        /// </summary>
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;

        /// <summary>
        /// Format we will use for the color stream
        /// </summary>
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        /// <summary>
        /// Height of the color image
        /// </summary>
        private int _colorHeight;

        private int _colorToDepthDivisor;

        /// <summary>
        /// Width of the color image
        /// </summary>
        private int _colorWidth;

        /// <summary>
        /// Height of the depth image
        /// </summary>
        private int _depthHeight;

        private int _depthStreamFramePixelDataLength;

        /// <summary>
        /// Width of the depth image
        /// </summary>
        private int _depthWidth;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor _kinectSensor;

        /// <summary>
        /// Initiate the sensor
        /// </summary>
        public KinectManager()
        {
            OnDepthImageFrame += (sender, args) => { };
            OnColorImageFrame += (sender, args) => { };
            OnSkeletonFrame += (sender, args) => { };

            InitializeSensor();

            //Handle Kinect status change such as disconnect and reconnect
            KinectSensor.KinectSensors.StatusChanged +=
                (s, eventArgs) =>
                    {
                        if (eventArgs.Status == KinectStatus.Connected)
                        {
                            if (_kinectSensor == null)
                            {
                                Debug.WriteLine("New Kinect connected");
                                InitializeSensor();
                            }
                            else
                            {
                                Debug.WriteLine(
                                    "Existing Kinect signalled connection");
                            }
                            return;
                        }

                        //else
                        if (eventArgs.Sensor == _kinectSensor)
                        {
                            Debug.WriteLine("Existing Kinect disconnected");
                            UninitializeSensor();
                        }
                        else
                        {
                            Debug.WriteLine("Other Kinect event occurred");
                        }
                    };
        }

        #region IKinectManager Members

        /// <summary>
        /// Set or check if the sensor is in Near mode
        /// </summary>
        public bool IsNearMode
        {
            get { return _kinectSensor != null && _kinectSensor.DepthStream.Range == DepthRange.Near; }
            set
            {
                if (_kinectSensor == null)
                    return;
                _kinectSensor.DepthStream.Range = value ? DepthRange.Near : DepthRange.Default;
            }
        }

        public event EventHandler<DepthImageFrameInfo> OnDepthImageFrame;
        public event EventHandler<ColorImageFrameInfo> OnColorImageFrame;
        public event EventHandler<SkeletonFrame> OnSkeletonFrame;

        /// <summary>
        /// Maps every point in a depth frame to the corresponding location in a ColorImageFormat coordinate space.
        /// 
        /// </summary>
        /// <param name="depthImageFormat">The depth format of the source.</param><param name="depthPixelData">The depth frame pixel data, as retrieved from DepthImageFrame.CopyPixelDataTo.
        ///             Must be equal in length to Width*Height of the depth format specified by depthImageFormat.
        ///             </param><param name="colorImageFormat">The desired target image format.</param><param name="colorCoordinates">The ColorImagePoint array to receive the data.  Each element will be be the result of mapping the
        ///             corresponding depthPixelDatum to the specified ColorImageFormat coordinate space.
        ///             Must be equal in length to depthPixelData.
        ///             </param>
        public void MapDepthFrameToColorFrame(DepthImageFormat depthImageFormat, short[] depthPixelData,
                                              ColorImageFormat colorImageFormat, ColorImagePoint[] colorCoordinates)
        {
            _kinectSensor.MapDepthFrameToColorFrame(depthImageFormat, depthPixelData, colorImageFormat, colorCoordinates);
        }

        public bool IsSensorReady
        {
            get { return _kinectSensor != null; }
        }

        #endregion

        private void InitializeSensor()
        {
            _kinectSensor = (from potentialSensor in KinectSensor.KinectSensors
                             where potentialSensor.Status == KinectStatus.Connected
                             select potentialSensor).FirstOrDefault();
            if (_kinectSensor == null)
                return;

            // Turn on the depth stream to receive depth frames
            _kinectSensor.DepthStream.Enable(DepthFormat);

            _depthWidth = _kinectSensor.DepthStream.FrameWidth;
            _depthHeight = _kinectSensor.DepthStream.FrameHeight;

            _kinectSensor.ColorStream.Enable(ColorFormat);

            _colorWidth = _kinectSensor.ColorStream.FrameWidth;
            _colorHeight = _kinectSensor.ColorStream.FrameHeight;

            // Turn on to get player masks
            _kinectSensor.SkeletonStream.Enable();

            _colorToDepthDivisor = _colorWidth/_depthWidth;

            _depthStreamFramePixelDataLength = _kinectSensor.DepthStream.FramePixelDataLength;

            // Add an event handler to be called whenever there is new depth frame data
            _kinectSensor.AllFramesReady += SensorAllFramesReady;

            // Start the sensor!
            try
            {
                _kinectSensor.Start();
            }
            catch (IOException exception)
            {
                _kinectSensor = null;
                Debug.WriteLine(exception, "Init Kinect Sensor");
            }
        }

        private void UninitializeSensor()
        {
            if (_kinectSensor != null)
            {
                _kinectSensor.AllFramesReady -= SensorAllFramesReady;
                _kinectSensor.Stop();
                _kinectSensor = null;
            }
        }

        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // in the middle of shutting down, so nothing to do
            if (_kinectSensor == null)
            {
                return;
            }

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    var depthPixels = new short[_kinectSensor.DepthStream.FramePixelDataLength];


                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyPixelDataTo(depthPixels);

                    //invoke the event in another task
                    Task.Run(
                        () =>
                        OnDepthImageFrame(this,
                                          new DepthImageFrameInfo(DepthFormat, depthPixels, _depthWidth, _depthHeight,
                                                                  _colorToDepthDivisor, _depthStreamFramePixelDataLength)));
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Allocate space to put the color pixels we'll create, try to recycle the buffer
                    var colorPixels = new byte[_kinectSensor.ColorStream.FramePixelDataLength];

                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(colorPixels);

                    //invoke the event in another task
                    Task.Run(
                        () =>
                        OnColorImageFrame(this,
                                          new ColorImageFrameInfo(ColorFormat, colorPixels, _colorWidth, _colorHeight,
                                                                  _colorToDepthDivisor)));
                }
            }

            SkeletonFrame skeletonFrame = e.OpenSkeletonFrame();

            if (skeletonFrame != null)
            {
                //invoke the event in another task
                Task.Run(() => OnSkeletonFrame(this, skeletonFrame));
            }
        }
    }
}