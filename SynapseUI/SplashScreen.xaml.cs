using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static SynapseUI.EventMapping.EventMap;

namespace SynapseUI
{
    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += (o, e) => { Initialize(); };
        }

        private void Initialize()
        {
            OpenMainWindow();
            return;
        }

        private void OpenMainWindow()
        {
            new ExecuteWindow().Show();
            this.Close();
        }

        // Window Events //
        private void DraggableTop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
