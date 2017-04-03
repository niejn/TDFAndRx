using Microsoft.Kinect;

namespace TDFKinectGreenScreen.Model
{
    /// <summary>
    /// The Kinect color sensor information frame
    /// </summary>
    public class ColorImageFrameInfo
    {
        private readonly ColorImageFormat _colorImageFormat;

        private readonly int _colorToDepthDivisor;
        private readonly byte[] _frameData;

        /// <summary>
        /// Height of the color image
        /// </summary>
        private readonly int _height;

        /// <summary>
        /// Width of the color image
        /// </summary>
        private readonly int _width;

        /// <summary>
        /// Initiate the frame fata fields
        /// </summary>
        /// <param name="colorImageFormat">The image map format</param>
        /// <param name="frameData">The raw data</param>
        /// <param name="width">The image width</param>
        /// <param name="height">The image height</param>
        /// <param name="colorToDepthDivisor">The aspect ration of color to depth map</param>
        public ColorImageFrameInfo(ColorImageFormat colorImageFormat, byte[] frameData, int width, int height,
                                   int colorToDepthDivisor)
        {
            _colorImageFormat = colorImageFormat;
            _frameData = frameData;
            _width = width;
            _height = height;
            _colorToDepthDivisor = colorToDepthDivisor;
        }

        /// <summary>
        /// The raw data
        /// </summary>
        public byte[] FrameData
        {
            get { return _frameData; }
        }

        /// <summary>
        /// Width of the color image
        /// </summary>
        public int Width
        {
            get { return _width; }
        }

        /// <summary>
        /// Height of the color image
        /// </summary>
        public int Height
        {
            get { return _height; }
        }

        /// <summary>
        /// The image map format
        /// </summary>
        public ColorImageFormat Format
        {
            get { return _colorImageFormat; }
        }

        /// <summary>
        /// The aspect ration of color to depth map
        /// </summary>
        public int ColorToDepthDivisor
        {
            get { return _colorToDepthDivisor; }
        }
    }
}