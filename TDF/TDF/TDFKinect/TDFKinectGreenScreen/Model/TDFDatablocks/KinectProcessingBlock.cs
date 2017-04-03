using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TDFKinectGreenScreen.Model.TDFDatablocks
{
    /// <summary>
    /// A base class for Kinect source blocks
    /// </summary>
    /// <typeparam name="T">The kinect sensor information type</typeparam>
    internal abstract class KinectProcessingBlock<T> : ISourceBlock<T>
    {
        //The source block for the Kinect sensor data
        private readonly BroadcastBlock<T> _broadcast = new BroadcastBlock<T>(i => i);

        /// <summary>
        /// Initiate the base class block
        /// </summary>
        /// <param name="kinectManager">The Kinect manager that provide the sensor data and manipulation</param>
        protected KinectProcessingBlock(IKinectManager kinectManager)
        {
            KinectManager = kinectManager;
        }

        /// <summary>
        /// The Kinect manager
        /// </summary>
        protected IKinectManager KinectManager { get; private set; }

        #region ISourceBlock<T> Members

        T ISourceBlock<T>.ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target, out bool messageConsumed)
        {
            return ((ISourceBlock<T>)_broadcast).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public IDisposable LinkTo(ITargetBlock<T> target, DataflowLinkOptions linkOptions)
        {
            return _broadcast.LinkTo(target, linkOptions);
        }

        void ISourceBlock<T>.ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
        {
            ((ISourceBlock<T>) _broadcast).ReleaseReservation(messageHeader, target);
        }

        bool ISourceBlock<T>.ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
        {
            return ((ISourceBlock<T>) _broadcast).ReserveMessage(messageHeader, target);
        }

        public void Complete()
        {
            _broadcast.Complete();
            OnComplete();
        }

        public Task Completion
        {
            get { return _broadcast.Completion; }
        }

        public void Fault(Exception exception)
        {
            ((ISourceBlock<T>) _broadcast).Fault(exception);
            OnComplete();
        }

        #endregion

        /// <summary>
        /// Send the sensor data to the next blocks
        /// </summary>
        /// <param name="message">The output sensor data</param>
        protected void SendAsync(T message)
        {
            _broadcast.SendAsync(message);
        }

        /// <summary>
        /// Template method to tell derived class that Complete or Fault had been called
        /// </summary>
        protected abstract void OnComplete();
    }
}