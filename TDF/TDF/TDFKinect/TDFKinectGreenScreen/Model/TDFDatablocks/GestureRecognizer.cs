using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Kinect;
using Microsoft.Samples.Kinect.SwipeGestureRecognizer;

namespace TDFKinectGreenScreen.Model.TDFDatablocks
{
    internal class GestureRecognizer : IPropagatorBlock<SkeletonFrame, BackgroundImageCommand>
    {
        /// <summary>
        /// The recognizer being used.
        /// </summary>
        private readonly Recognizer _activeRecognizer;

        private readonly BroadcastBlock<BackgroundImageCommand> _broadcastBlock =
            new BroadcastBlock<BackgroundImageCommand>(i => i);

        private readonly ActionBlock<SkeletonFrame> _skeletonBlock;
        private bool _areHandsClose;

        /// <summary>
        /// The ID if the skeleton to be tracked.
        /// </summary>
        private int _nearestId = -1;

        public GestureRecognizer()
        {
            _skeletonBlock = new ActionBlock<SkeletonFrame>(f => OnNewSkeletonFrame(f));
            _activeRecognizer = CreateRecognizer();
        }

        #region IPropagatorBlock<SkeletonFrame,BackgroundImageCommand> Members

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, SkeletonFrame messageValue,
                                                  ISourceBlock<SkeletonFrame> source, bool consumeToAccept)
        {
            return ((ITargetBlock<SkeletonFrame>) _skeletonBlock).OfferMessage(messageHeader, messageValue, source,
                                                                               consumeToAccept);
        }

        public void Complete()
        {
            _skeletonBlock.Complete();
            _broadcastBlock.Complete();
        }

        public Task Completion
        {
            get { return _skeletonBlock.Completion; }
        }

        void IDataflowBlock.Fault(Exception exception)
        {
            ((IDataflowBlock) _skeletonBlock).Fault(exception);
            ((IDataflowBlock) _broadcastBlock).Fault(exception);
        }

        public BackgroundImageCommand ConsumeMessage(DataflowMessageHeader messageHeader,
                                                     ITargetBlock<BackgroundImageCommand> target,
                                                     out bool messageConsumed)
        {
            return ((ISourceBlock<BackgroundImageCommand>) _broadcastBlock).ConsumeMessage(messageHeader, target,
                                                                                           out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<BackgroundImageCommand> target, DataflowLinkOptions linkOptions)
        {
            return _broadcastBlock.LinkTo(target, linkOptions);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<BackgroundImageCommand> target)
        {
            ((ISourceBlock<BackgroundImageCommand>) _broadcastBlock).ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<BackgroundImageCommand> target)
        {
            return ((ISourceBlock<BackgroundImageCommand>) _broadcastBlock).ReserveMessage(messageHeader, target);
        }

        #endregion

        /// <summary>
        /// Create a wired-up recognizer for running the slideshow.
        /// </summary>
        /// <returns>The wired-up recognizer.</returns>
        private Recognizer CreateRecognizer()
        {
            // Instantiate a recognizer.
            var recognizer = new Recognizer();

            // Wire-up swipe right to manually advance picture.
            recognizer.SwipeRightDetected += (s, e) =>
                                                 {
                                                     if (e.Skeleton.TrackingId == _nearestId)
                                                     {
                                                         _broadcastBlock.SendAsync(BackgroundImageCommand.NextImage);
                                                     }
                                                 };

            // Wire-up swipe left to manually reverse picture.
            recognizer.SwipeLeftDetected += (s, e) =>
                                                {
                                                    if (e.Skeleton.TrackingId == _nearestId)
                                                    {
                                                        _broadcastBlock.SendAsync(BackgroundImageCommand.PreviousImage);
                                                    }
                                                };

            return recognizer;
        }


        private void OnNewSkeletonFrame(SkeletonFrame skeletonFrame)
        {
            try
            {
                var skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                skeletonFrame.CopySkeletonDataTo(skeletons);

                // Assume no nearest skeleton and that the nearest skeleton is a long way away.
                int newNearestId = -1;
                double nearestDistance2 = double.MaxValue;

                Skeleton selectedSkeleton = null;

                // Look through the skeletons.
                foreach (Skeleton skeleton in skeletons)
                {
                    if (selectedSkeleton == null)
                        selectedSkeleton = skeleton;

                    // Only consider tracked skeletons.
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        // Find the distance squared.
                        float distance2 = (skeleton.Position.X*skeleton.Position.X) +
                                          (skeleton.Position.Y*skeleton.Position.Y) +
                                          (skeleton.Position.Z*skeleton.Position.Z);

                        // Is the new distance squared closer than the nearest so far?
                        if (distance2 < nearestDistance2)
                        {
                            // Use the new values.
                            newNearestId = skeleton.TrackingId;
                            nearestDistance2 = distance2;
                            selectedSkeleton = skeleton;
                        }
                    }
                }

                _nearestId = newNearestId;

                // Pass skeletons to recognizer.
                _activeRecognizer.Recognize(this, skeletonFrame, skeletons);

 
                if (selectedSkeleton != null &&
                    IsVeryClose(selectedSkeleton.Joints[JointType.HandLeft].Position.Y,
                    selectedSkeleton.Joints[JointType.ShoulderLeft].Position.Y) &&
                    IsVeryClose(selectedSkeleton.Joints[JointType.HandRight].Position.Y,
                    selectedSkeleton.Joints[JointType.ShoulderRight].Position.Y) &&
                    Math.Abs(selectedSkeleton.Joints[JointType.HandLeft].Position.X - 
                                selectedSkeleton.Joints[JointType.HandRight].Position.X) < 0.5 &&
                    IsVeryClose(selectedSkeleton.Joints[JointType.HandLeft].Position.Y,
                                selectedSkeleton.Joints[JointType.HandRight].Position.Y) &&
                    IsVeryClose(selectedSkeleton.Joints[JointType.HandLeft].Position.Z,
                                selectedSkeleton.Joints[JointType.HandRight].Position.Z))
                {
                    if (_areHandsClose)
                        return;
                    _areHandsClose = true;
                    _broadcastBlock.SendAsync(BackgroundImageCommand.ToGreenScreen);
                }
                else if (_areHandsClose)
                {
                    _areHandsClose = false;
                    _broadcastBlock.SendAsync(BackgroundImageCommand.FromGreenScreen);
                }
            }
            finally
            {
                skeletonFrame.Dispose();
            }
        }


        private bool IsVeryClose(float p0, float p1)
        {
            return Math.Abs(p0 - p1) < 0.1;
        }
    }
}