namespace TDFKinectGreenScreen.Model.TDFDatablocks
{
    /// <summary>
    /// Represents the Kinect color camera sensor as a block
    /// </summary>
    internal class ColorCameraBlock : KinectProcessingBlock<ColorImageFrameInfo>
    {
        /// <summary>
        /// Initiate the color camera block
        /// </summary>
        /// <param name="kinectManager">The Kinect Manager</param>
        public ColorCameraBlock(IKinectManager kinectManager)
            : base(kinectManager)
        {
            KinectManager.OnColorImageFrame += KinectManagerOnColorImageFrame;
        }

        /// <summary>
        /// A color camera frame callback
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="colorImageFrameInfo"></param>
        private void KinectManagerOnColorImageFrame(object sender, ColorImageFrameInfo colorImageFrameInfo)
        {
            SendAsync(colorImageFrameInfo);
        }

        /// <summary>
        /// BLock no longer need to receive data
        /// </summary>
        protected override void OnComplete()
        {
            KinectManager.OnColorImageFrame -= KinectManagerOnColorImageFrame;
        }
    }
}