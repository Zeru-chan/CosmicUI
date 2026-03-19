using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.Threading;
using SynapseUI.Types;
using SynapseUI.Functions.Utils;
using SynapseUI.Controls.AceEditor;

namespace SynapseUI
{
    public partial class OptionsWindow : Window
    {
        public static Mutex RobloxMutex;

        private static bool _mutexActive = false;

        public OptionsEntryList OptionsList { get; } = new OptionsEntryList();

        private bool _firstLoad = true;
        private readonly AceEditor _aceEditor;
        private readonly Options _options;

        public OptionsWindow(ExecuteWindow main, AceEditor editor)
        {
            InitializeComponent();
            _aceEditor = editor;
            _options = main.SynOptions;

            Left = main.Left + (main.ActualWidth - Width) / 2;
            Top = main.Top + 10;

            Closing += (s, e) =>
            {
                PersistSettings();
            };
        }

        private void OptionsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AnimateShow();

            mutexToggle.IsToggled = _mutexActive;
            aceThemesComboBox.SelectedItem = App.SETTINGS.AceTheme;
            roundedCornerToggle.IsToggled = App.SETTINGS.RoundedCorners;

            _firstLoad = false;
        }

        private void PersistSettings()
        {
            App.SETTINGS.CaptureFrom(_options);
            App.SETTINGS.Save();
        }

        private void AnimateShow()
        {
            var stry = (Storyboard)FindResource("moveWindowAnimation");
            var anim = (DoubleAnimation)stry.Children[0];

            anim.From = Top;
            anim.To = Top - 10;

            stry.Begin();
        }

        public static void DisposeMutex()
        {
            if (RobloxMutex != null)
            {
                RobloxMutex.ReleaseMutex();
                RobloxMutex.Close();
                RobloxMutex.Dispose();

                RobloxMutex = null;
            }
        }

        public event OptionChangedEventHandler OptionChanged;

        protected virtual void OnOptionChanged(OptionChangedEventArgs e)
        {
            OptionChanged?.Invoke(this, e);
        }

        private void SliderToggle_ToggledStatusChanged(object sender, Controls.ToggledStatusChangedEventArgs e)
        {
            var slider = sender as Controls.SliderToggle;
            OptionEntry entry = (OptionEntry)slider.DataContext;

            _options.SetProperty(entry.Name, e.Value);
            if (!_firstLoad)
            {
                PersistSettings();
                OnOptionChanged(new OptionChangedEventArgs(entry, e.Value));
            }
        }

        private void OptionToggle_Loaded(object sender, RoutedEventArgs e)
        {
            var slider = sender as Controls.SliderToggle;
            OptionEntry entry = (OptionEntry)slider?.DataContext;
            if (slider == null || entry == null)
                return;

            slider.IsToggled = _options.GetProperty(entry.Name);
        }

        private void Execute_ScriptButton(object sender, MouseButtonEventArgs e)
        {
        }

        private void KillRobloxButton_Click(object sender, RoutedEventArgs e)
        {
            var processes = Process.GetProcessesByName("RobloxPlayerBeta");
            foreach (var process in processes)
            {
                process.Kill();
            }
        }

        private void MultiRBLX_ToggledStatusChanged(object sender, Controls.ToggledStatusChangedEventArgs e)
        {
            if (e.Value)
            {
                if (RobloxMutex is null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RobloxMutex = new Mutex(true, "ROBLOX_singletonMutex");
                    });
                }
            }
            else
            {
                DisposeMutex();
            }

            _mutexActive = e.Value;
        }

        private void RoundedCorner_ToggledStatusChanged(object sender, Controls.ToggledStatusChangedEventArgs e)
        {
            App.SETTINGS.RoundedCorners = e.Value;
            if (!_firstLoad)
                PersistSettings();
        }

        private void AceThemesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string theme = (string)e.AddedItems[0];
                if (string.Equals(theme, App.SETTINGS.AceTheme, StringComparison.OrdinalIgnoreCase) && !_firstLoad)
                    return;

                App.SETTINGS.AceTheme = theme;
                _aceEditor?.SetTheme(NormalizeThemeName(theme));
                if (!_firstLoad)
                    PersistSettings();
            }
        }

        private static string NormalizeThemeName(string theme)
        {
            return new StringBuilder(theme ?? "Twilight")
                .Replace('-', '_')
                .Remove(0, 1)
                .Insert(0, char.ToLower((theme ?? "Twilight")[0]))
                .ToString();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DraggableTop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }

    public delegate void OptionChangedEventHandler(object sender, OptionChangedEventArgs e);

    public class OptionChangedEventArgs : EventArgs
    {
        public OptionEntry Entry { get; }
        public bool Value { get; }
        public OptionChangedEventArgs(OptionEntry entry, bool value)
        {
            Entry = entry;
            Value = value;
        }
    }
}
