using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Registration;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Media.Imaging;
using TDFKinectGreenScreen.Model;
using TDFKinectGreenScreen.Model.TDFDatablocks;
using TDFKinectGreenScreen.ViewModel;

namespace TDFKinectGreenScreen
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
// ReSharper disable RedundantExtendsListEntry
    public partial class App : Application
// ReSharper restore RedundantExtendsListEntry
    {
        private CompositionContainer _container;

        public CompositionContainer Container
        {
            get { return _container; }
        }

        /// <summary>
        /// Use MEF to handle View/ViewModel/Model creation and binding
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var conventions = new RegistrationBuilder();
            conventions.ForType<KinectManager>()
                .Export<IKinectManager>()
                .SetCreationPolicy(CreationPolicy.Shared);

            conventions.ForType<KinectGreenScreenViewModel>()
                .Export<IKinectGreenScreenViewModel>()
                .SetCreationPolicy(CreationPolicy.Shared);

            conventions.ForType<KinectGreenScreenViewModel>().ImportProperty(p => p.KinectManager);

            Assembly assembly = Assembly.GetExecutingAssembly();
            var catalog = new AssemblyCatalog(assembly, conventions);
            _container = new CompositionContainer(catalog);
            Container.ComposeParts();

            ISourceBlock<BitmapSource> imageSource = NetworkBuilder.Build(_container.GetExportedValue<IKinectManager>());
           
            _container.GetExportedValue<IKinectGreenScreenViewModel>().GreenScreenImageGenerator = imageSource;

        }
    }
}