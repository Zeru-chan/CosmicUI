using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SynapseUI.Types;
using SynapseUI.Controls.AceEditor;
using SynapseUI.Functions.Web;

namespace SynapseUI
{
    /// <summary>
    /// Interaction logic for ExecuteWindow.xaml
    /// </summary>
    public partial class ExecuteWindow : Window
    {
        public Options SynOptions { get; set; } = new Options();

        private AceEditor Editor;
        private FileSystemWatcher ScriptWatcher;

        private bool _optionWindowOpened = false;

        public ExecuteWindow()
        {
            InitializeComponent();
            App.SETTINGS.ApplyTo(SynOptions);
            Cosmic.OnClientConnected += Cosmic_OnClientConnected;
            Cosmic.OnClientDisconnected += Cosmic_OnClientDisconnected;

            if (Directory.Exists("./scripts"))
            {
                ScriptWatcher = new FileSystemWatcher("./scripts");
                ScriptWatcher.Created += ScriptWatcher_Created;
                ScriptWatcher.Deleted += ScriptWatcher_Deleted;
                ScriptWatcher.Renamed += ScriptWatcher_Renamed;
                ScriptWatcher.Filter = "*";

                ScriptWatcher.EnableRaisingEvents = true;
            }
        }

        private void ExecutorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var file in new DirectoryInfo(@".\scripts\").GetFiles())
            {
                if (file.Extension == ".txt" || file.Extension == ".lua")
                    scriptsListBox.Items.Add(file.Name);
            }

            UpdateAttachIndicator();
            _ = LoadVersionInfo();

            if (!App.SKIP_CEF)
                LoadCefBrowser();
        }

        private void ExecuteWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Cosmic.OnClientConnected -= Cosmic_OnClientConnected;
            Cosmic.OnClientDisconnected -= Cosmic_OnClientDisconnected;
            SaveScriptTabs();
            OptionsWindow.DisposeMutex();

            if (!App.DEBUG)
                Cosmic.Shutdown();

            Environment.Exit(0);
        }
        
        private void LoadCefBrowser()
        {
            if (!File.Exists(@".\bin\custom\Editor.html"))
                return;

            var editorPath = Path.Combine(App.CURRENT_DIR, "bin", "custom", "Editor.html");
            var editorUrl = new Uri(editorPath).AbsoluteUri;
            var editor = new AceEditor(editorUrl, scriptsTabPanel);
            cefSharpGrid.Children.Add(editor);

            Editor = editor;

            Editor.FrameLoadEnd += (s, args) =>
            {
                if (args.Frame.IsMain)
                {
                    LoadScriptTabs();
                    ApplySavedTheme();
                }
            };

            Editor.Service.SaveFileRequest += async (s, args) =>
            {
                await Dispatcher.InvokeAsync(AlertFileSave);
            };

            scriptsTabPanel.RequestedTabClose += BeforeScriptTabDelete;
        }
        
        private void LoadScriptTabs()
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                bool empty = Editor.OpenScriptsFromXML();
                if (empty)
                    scriptsTabPanel.AddScript(true);

                scriptsPanel.Visibility = Visibility.Visible;
            }));
        }

        private void SaveScriptTabs()
        {
            if (Editor == null || scriptsTabPanel.SelectedItem == null)
                return;

            var scripts = new List<Script>();

            var selectedTab = (Controls.ScriptTab)scriptsTabPanel.SelectedItem;
            Editor.ScriptMap[selectedTab.Header] = Editor.GetText();

            foreach (var entry in Editor.ScriptMap)
            {
                string scriptPath = Editor.ScriptPathMap.TryGetValue(entry.Key, out string path) ? path : "";
                scripts.Add(new Script(entry.Key, scriptPath, entry.Value));
            }

            TabSaver.SaveToXML(scripts, scriptsTabPanel.DefaultIndex);
        }

        private void ApplySavedTheme()
        {
            var savedTheme = NormalizeThemeName(App.SETTINGS.AceTheme);
            Editor?.SetTheme(savedTheme);
        }

        private static string NormalizeThemeName(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
                return "twilight";

            return theme
                .Replace('-', '_')
                .ToLowerInvariant();
        }

        private async Task AlertFileSave()
        {
            statusInfoLabel.Content = "Saved file.";
            await statusInfoLabel.SetActive(true);
            await statusInfoLabel.SetActive(false);
        }

        private async Task ShowAttachStatus(string message)
        {
            attachInfoLabel.Content = message;
            await attachInfoLabel.SetActive(true);
            await attachInfoLabel.SetActive(false);
        }

        private async Task LoadVersionInfo()
        {
            headerVersionLabel.Content = "vloading...";

            var version = await VersionChecker.GetLatestVersionAsync();
            if (!string.IsNullOrWhiteSpace(version))
            {
                headerVersionLabel.Content = version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : $"v{version}";
                return;
            }

            headerVersionLabel.Content = $"v{VersionChecker.GetCurrentVersion()}";
        }

        private void Cosmic_OnClientConnected(int pid)
        {
            Dispatcher.BeginInvoke(new Action(UpdateAttachIndicator));
        }

        private void Cosmic_OnClientDisconnected(int pid)
        {
            Dispatcher.BeginInvoke(new Action(UpdateAttachIndicator));
        }

        private void UpdateAttachIndicator()
        {
            int clientCount = Cosmic.ClientCount;
            bool isAttached = clientCount > 0;
            attachStateIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isAttached ? "#FF45D06F" : "#FFE14B4B"));
            attachStateLabel.Content = isAttached
                ? $"Attached to {clientCount} client{(clientCount == 1 ? string.Empty : "s")}"
                : "Waiting for attach";
        }

        private void BeforeScriptTabDelete(object sender, EventArgs args)
        {
            var tab = sender as Controls.ScriptTab;

            if (SynOptions.CloseConfirmation && scriptsTabPanel.Items.Count != 1)
            {
                if (tab == scriptsTabPanel.SelectedTab)
                {
                    if (!Editor.IsEmpty())
                    {
                        bool res = new ConfirmationWindow("Are you sure you want to close this script? All changes will be lost!").ShowDialog();
                        if (!res) return;
                    }
                }
                else
                {
                    if (Editor.ScriptMap[tab.Header].Length != 0)
                    {
                        bool res = new ConfirmationWindow("Are you sure you want to close this script? All changes will be lost!").ShowDialog();
                        if (!res) return;
                    }
                }
            }

            tab.Close();
        }

        // Script watcher events //
        private void ScriptWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (!e.Name.EndsWith(".lua") && !e.Name.EndsWith(".txt"))
                return;

            Dispatcher.BeginInvoke(new Action(delegate
            {
                int i = scriptsListBox.Items.IndexOf(e.Name);
                if (i != -1)
                    scriptsListBox.Items.RemoveAt(i);
            }));
        }

        private void ScriptWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (!e.Name.EndsWith(".lua") && !e.Name.EndsWith(".txt"))
                return;

            Dispatcher.BeginInvoke(new Action(() => scriptsListBox.Items.Add(e.Name)));
        }

        private void ScriptWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (!e.Name.EndsWith(".lua") && !e.Name.EndsWith(".txt"))
                return;

            Dispatcher.BeginInvoke(new Action(delegate
            {
                int i = scriptsListBox.Items.IndexOf(e.OldName);
                if (i != -1)
                {
                    scriptsListBox.Items.RemoveAt(i);
                    scriptsListBox.Items.Add(e.Name);
                }
            }));
        }

        // Button Events //
        private void OpenOptions_Click(object sender, RoutedEventArgs e)
        {
            if (_optionWindowOpened)
                return;

            var p = new OptionsWindow(this, Editor);
            p.Closed += (s, ev) => { _optionWindowOpened = false; };
            p.OptionChanged += (s, ev) => { SynOptions.SetProperty(ev.Entry.Name, ev.Value); };

            p.Show();

            _optionWindowOpened = true;
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var processes = Cosmic.GetRobloxProcesses();
                if (processes.Count == 0)
                {
                    _ = ShowAttachStatus("No Roblox processes found.");
                    return;
                }

                var alreadyInjected = Cosmic.GetClients();
                if (processes.TrueForAll(pid => alreadyInjected.Contains(pid)))
                {
                    _ = ShowAttachStatus("Already injected.");
                    return;
                }

                var results = Cosmic.Attach();
                UpdateAttachIndicator();
                if (results.Count == 0)
                {
                    _ = ShowAttachStatus("Already injected.");
                }
                else
                {
                    var first = System.Linq.Enumerable.First(results.Values);
                    _ = ShowAttachStatus(Cosmic.GetAttachStatusMessage(first));
                }
            }
            catch (Exception ex)
            {
                _ = ShowAttachStatus(ex.Message);
            }
        }

        private async void SaveFileButton_Click(object sender, RoutedEventArgs e)
        {
            Editor?.SaveScript();
            await AlertFileSave();
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            Editor?.OpenScript();
        }

        private void ExecuteFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (Cosmic.ClientCount == 0)
            {
                _ = ShowAttachStatus("Not injected!");
                return;
            }

            var diag = Functions.Utils.Dialog.OpenFileDialog();
            switch (diag.ShowDialog())
            {
                case true:
                    Cosmic.Execute(File.ReadAllText(diag.FileName));
                    break;

                case false:
                default:
                    break;
            }
        }

        private void ClearEditorButton_Click(object sender, RoutedEventArgs e)
        {
            if (SynOptions.ClearConfirmation)
            {
                bool res = new ConfirmationWindow("Are you sure you want to clear the editor? All changes will be lost!").ShowDialog();
                if (!res) return;
            }

            Editor?.ClearEditor();
        }

        private void ExecuteEditorButton_Click(object sender, RoutedEventArgs e)
        {
            if (Cosmic.ClientCount == 0)
            {
                _ = ShowAttachStatus("Not injected!");
                return;
            }

            string contents = Editor?.GetText() ?? null;
            if (contents != null)
                Cosmic.Execute(contents);
        }

        private void ExecuteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Cosmic.ClientCount == 0)
            {
                _ = ShowAttachStatus("Not injected!");
                return;
            }

            if (scriptsListBox.SelectedIndex == -1)
                return;

            string path = @".\scripts\" + (string)scriptsListBox.SelectedItem;
            if (File.Exists(path))
                Cosmic.Execute(File.ReadAllText(path));
            else
                scriptsListBox.Items.RemoveAt(scriptsListBox.SelectedIndex);
        }

        private void LoadEditorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (scriptsListBox.SelectedIndex == -1)
                return;

            string path = @".\scripts\" + (string)scriptsListBox.SelectedItem;
            if (File.Exists(path))
                Editor?.SetText(File.ReadAllText(path));
            else
                scriptsListBox.Items.RemoveAt(scriptsListBox.SelectedIndex);
        }

        private void LoadFileIntoEditor_Click(object sender, RoutedEventArgs e)
        {
            if (scriptsListBox.SelectedIndex == -1)
                return;

            string script = (string)scriptsListBox.SelectedItem;
            string path = Path.Combine(App.CURRENT_DIR, "scripts", script);
            if (File.Exists(path))
                Editor?.OpenScriptFile(script, path);
            else
                scriptsListBox.Items.RemoveAt(scriptsListBox.SelectedIndex);
        }

        private void AddScript_Click(object sender, RoutedEventArgs e)
        {
            scriptsTabPanel.AddScript();
        }

        // Window Events //
        private void ScriptsListBox_LostFocus(object sender, RoutedEventArgs e)
        {
            scriptsListBox.SelectedItem = null;
        }

        private void DraggableTop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MinimiseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
