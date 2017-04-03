namespace TDFKinectGreenScreen.Model.TDFDatablocks
{
    /// <summary>
    /// Represents the Kinect depth camera sensor as a block
    /// </summary>
    internal class DepthCameraBlock : KinectProcessingBlock<DepthImageFrameInfo>
    {
        /// <summary>
        /// Initiate the Depth camera block
        /// </summary>
        /// <param name="kinectManager">The Kinect Manager</param>
        public DepthCameraBlock(IKinectManager kinectManager)
            : base(kinectManager)
        {
            KinectManager.OnDepthImageFrame += KinectManagerOnDepthImageFrame;
        }

        /// <summary>
        /// A Depth camera frame callback
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="depthImageFrameInfo"></param>
        private void KinectManagerOnDepthImageFrame(object sender, DepthImageFrameInfo depthImageFrameInfo)
        {
            SendAsync(depthImageFrameInfo);
        }

        /// <summary>
        /// BLock no longer need to receive data
        /// </summary>
        protected override void OnComplete()
        {
            KinectManager.OnDepthImageFrame -= KinectManagerOnDepthImageFrame;
        }
    }
}