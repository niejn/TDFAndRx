using Microsoft.Kinect;

namespace TDFKinectGreenScreen.Model.TDFDatablocks
{
    /// <summary>
    /// Represents the Kinect skeleton source data as a block
    /// </summary>
    internal class SkeletonBlock : KinectProcessingBlock<SkeletonFrame>
    {
        /// <summary>
        /// Initiate the skeleton source block
        /// </summary>
        /// <param name="kinectManager">The Kinect Manager</param>
        public SkeletonBlock(IKinectManager kinectManager)
            : base(kinectManager)
        {
            KinectManager.OnSkeletonFrame += KinectManagerOnSkeletonFrame;
        }

        /// <summary>
        /// A skeleton frame callback
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="skeletonFrame"></param>
        private void KinectManagerOnSkeletonFrame(object sender, SkeletonFrame skeletonFrame)
        {
            SendAsync(skeletonFrame);
        }

        /// <summary>
        /// BLock no longer need to receive data
        /// </summary>
        protected override void OnComplete()
        {
            KinectManager.OnSkeletonFrame -= KinectManagerOnSkeletonFrame;
        }
    }
}