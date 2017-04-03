using Microsoft.Kinect;

namespace TDFKinectGreenScreen.Model
{
    public class DepthImageFrameInfo
    {
        private readonly int _colorToDepthDivisor;

        private readonly int _depthStreamFramePixelDataLength;
        private readonly DepthImageFormat _format;
        private readonly short[] _frameData;

        /// <summary>
        /// Height of the depth image
        /// </summary>
        private readonly int _height;

        /// <summary>
        /// Width of the depth image
        /// </summary>
        private readonly int _width;

        /// <summary>
        /// Initiate the depth frame data
        /// </summary>
        /// <param name="format">The depth map format</param>
        /// <param name="frameData">The raw depth data</param>
        /// <param name="depthWidth">Width of the depth image</param>
        /// <param name="height">Height of the depth image</param>
        /// <param name="colorToDepthDivisor">The aspect ration of color to depth map</param>
        /// <param name="depthStreamFramePixelDataLength">The buffer length</param>
        public DepthImageFrameInfo(DepthImageFormat format, short[] frameData, int depthWidth, int height,
                                   int colorToDepthDivisor, int depthStreamFramePixelDataLength)
        {
            _format = format;
            _frameData = frameData;
            _width = depthWidth;
            _height = height;
            _colorToDepthDivisor = colorToDepthDivisor;
            _depthStreamFramePixelDataLength = depthStreamFramePixelDataLength;
        }

        /// <summary>
        /// The depth map format
        /// </summary>
        public DepthImageFormat Format
        {
            get { return _format; }
        }

        /// <summary>
        /// The raw depth data
        /// </summary>
        public short[] FrameData
        {
            get { return _frameData; }
        }

        /// <summary>
        /// Width of the depth image
        /// </summary>
        public int Width
        {
            get { return _width; }
        }

        /// <summary>
        /// Height of the depth image
        /// </summary>
        public int Height
        {
            get { return _height; }
        }

        /// <summary>
        /// The aspect ration of color to depth map
        /// </summary>
        public int ColorToDepthDivisor
        {
            get { return _colorToDepthDivisor; }
        }

        /// <summary>
        /// The buffer length
        /// </summary>
        public int DepthStreamFramePixelDataLength
        {
            get { return _depthStreamFramePixelDataLength; }
        }
    }
}