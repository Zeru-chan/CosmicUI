using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SynapseUI
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += async (o, e) => { await Initialize(); };
        }

        private async Task Initialize()
        {
            if (App.DEBUG)
            {
                OpenMainWindow();
                return;
            }

            statusLabel.Content = "Initializing Cosmic...";

            try
            {
                await Cosmic.Initialize();
                loadingBar.AnimateFinish();
                await Task.Delay(500);
                OpenMainWindow();
            }
            catch (Exception ex)
            {
                new ErrorWindow(new Types.BaseError(Exceptions.BaseException.GENERIC_EXCEPTION), ex.Message).Show();
                Close();
            }
        }

        private void OpenMainWindow()
        {
            new ExecuteWindow().Show();
            Close();
        }

        private void DraggableTop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
