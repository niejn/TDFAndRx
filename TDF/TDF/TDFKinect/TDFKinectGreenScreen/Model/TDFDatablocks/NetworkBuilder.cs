using System.Threading.Tasks.Dataflow;
using System.Windows.Media.Imaging;
using TDFKinectGreenScreen.Model.TDFDatablocks;

namespace TDFKinectGreenScreen.Model.TDFDatablocks
{
    internal static class NetworkBuilder
    {
        /// <summary>
        /// Build the network
        /// </summary>
        /// <param name="kinectManager">The kinect manager</param>
        /// <returns>The result image block. The last in the chain</returns>
        public static ISourceBlock<BitmapSource> Build(IKinectManager kinectManager)
        {
            var skeletonBlock = new SkeletonBlock(kinectManager);
            var gestureRecognizer = new GestureRecognizer();
            skeletonBlock.LinkTo(gestureRecognizer, new DataflowLinkOptions {PropagateCompletion = true});

            var backgroundPictureManagerBlock = new BackgroundPictureManagerBlock();
            gestureRecognizer.LinkTo(backgroundPictureManagerBlock);

            var greenTransformBlock = new GreenTransformBlock();
            backgroundPictureManagerBlock.LinkTo(greenTransformBlock,
                                                 new DataflowLinkOptions {PropagateCompletion = true});

            gestureRecognizer.LinkTo(greenTransformBlock.TargetCommand);

            var depthCameraBlock = new DepthCameraBlock(kinectManager);
            var colorCameraBlock = new ColorCameraBlock(kinectManager);
            var combineImageBlock = new ComposeImagesBlock(kinectManager);

            depthCameraBlock.LinkTo(combineImageBlock.DepthCameraTarget,
                                    new DataflowLinkOptions {PropagateCompletion = true});
            colorCameraBlock.LinkTo(combineImageBlock.ColorCameraTarget,
                                    new DataflowLinkOptions {PropagateCompletion = true});


            greenTransformBlock.LinkTo(combineImageBlock.ImageTarget,
                                       new DataflowLinkOptions {PropagateCompletion = true});

            return combineImageBlock;
        }
    }
}