using System;
using Microsoft.Kinect;

namespace TDFKinectGreenScreen.Model
{
    public interface IKinectManager
    {
        /// <summary>
        /// Set or check if the sensor is in Near mode
        /// </summary>
        bool IsNearMode { get; set; }

        /// <summary>
        /// The kinect sensor is ready to send events
        /// </summary>
        bool IsSensorReady { get; }

        /// <summary>
        /// A depth image frame is ready to conume
        /// </summary>
        event EventHandler<DepthImageFrameInfo> OnDepthImageFrame;

        /// <summary>
        /// A color image frame is ready to consum
        /// </summary>
        event EventHandler<ColorImageFrameInfo> OnColorImageFrame;

        /// <summary>
        /// Skeleton data is ready to consume
        /// </summary>
        event EventHandler<SkeletonFrame> OnSkeletonFrame;

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
        void MapDepthFrameToColorFrame(DepthImageFormat depthImageFormat, short[] depthPixelData, ColorImageFormat colorImageFormat,
                                       ColorImagePoint[] colorCoordinates);
    }
}