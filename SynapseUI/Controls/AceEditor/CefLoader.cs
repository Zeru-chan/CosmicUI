using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;

namespace SynapseUI.Controls.AceEditor
{
    public static class CefLoader
    {
        public static bool Init()
        {
            if (Cef.IsInitialized == true) return true;

            var settings = new CefSettings
            {
                CachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GPUCache"),
                LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log"),
                IgnoreCertificateErrors = true
            };

            var success = Cef.Initialize(settings, performDependencyCheck: true, cefApp: null);
            return success;
        }
    }

    public class CefSharpService
    {
        public event EventHandler<SaveFileEventArgs> SaveFileRequest;
        public event EventHandler<SaveFileEventArgs> SaveAsRequest;
        public event EventHandler OpenFileRequest;

        public void saveFileRequest(string contents) => SaveFileRequest?.Invoke(this, new SaveFileEventArgs(contents));
        public void saveAsRequest(string contents) => SaveAsRequest?.Invoke(this, new SaveFileEventArgs(contents));
        public void openFileRequest() => OpenFileRequest?.Invoke(this, EventArgs.Empty);
    }

    public class SaveFileEventArgs : EventArgs
    {
        public string Value { get; }
        public SaveFileEventArgs(string value) => Value = value;
    }

    public class AceEditor : ChromiumWebBrowser
    {
        public ScriptsTabPanel ScriptsPanel;
        public CefSharpService Service = new CefSharpService();
        public Dictionary<string, string> ScriptMap = new Dictionary<string, string>();
        public Dictionary<string, string> ScriptPathMap = new Dictionary<string, string>();

        public AceEditor(string url, ScriptsTabPanel scriptsTab) : base(url)
        {
            this.Focusable = true;
            this.MinWidth = 100;
            this.MinHeight = 100;
            ScriptsPanel = scriptsTab;

            CefSharpSettings.WcfEnabled = true;
            JavascriptObjectRepository.Register("cefServiceAsync", Service, true, BindingOptions.DefaultBinder);

            Service.OpenFileRequest += (o, e) => OpenScript();
            Service.SaveFileRequest += (o, e) => SaveScript(e.Value);
            Service.SaveAsRequest += (o, e) => SaveScript(e.Value, true);

            ScriptsPanel.SelectedScriptChanged += ScriptTabChanged;
            ScriptsPanel.ScriptTabClosed += ScriptTabClosed;
            ScriptsPanel.ScriptTabAdded += ScriptTabAdded;

            this.PreviewMouseDown += (s, e) => this.Focus();
        }

        public void SetText(string contents) => this.ExecuteScriptAsync("SetText", contents);

        public string GetText()
        {
            var task = this.EvaluateScriptAsync("GetText()");
            task.Wait(500);
            return (task.Result.Success && task.Result.Result != null) ? task.Result.Result.ToString() : "";
        }

        public void ClearEditor() => SetText("");

        public bool IsEmpty() => string.IsNullOrEmpty(GetText());

        public void SetTheme(string theme) => this.ExecuteScriptAsync("SetTheme", theme);

        public void OpenScript()
        {
            var diag = Functions.Utils.Dialog.OpenFileDialog();
            if (diag.ShowDialog() == true)
            {
                Dispatcher.Invoke(() => OpenScriptFile(diag.SafeFileName, diag.FileName));
            }
        }

        public void OpenScriptFile(string filename, string path)
        {
            if (ScriptPathMap.ContainsValue(path)) return;
            string contents = File.ReadAllText(path);

            if (!ScriptMap.ContainsKey(filename))
            {
                ScriptMap.Add(filename, contents);
                ScriptPathMap.Add(filename, path);
            }

            ScriptsPanel.AddScript(filename, path, true);
            ScriptMap[filename] = contents;
            SetText(contents);
        }

        public void SaveScript(string contents = null, bool saveAs = false)
        {
            Dispatcher.Invoke(() =>
            {
                var tab = ScriptsPanel.SelectedTab;
                if (tab == null) return;

                string textToSave = contents ?? GetText();

                if (string.IsNullOrEmpty(tab.FilePath) || saveAs)
                {
                    var diag = Functions.Utils.Dialog.SaveFileDialog();
                    if (diag.ShowDialog() == true)
                    {
                        if (ScriptMap.ContainsKey(tab.Header)) ScriptMap.Remove(tab.Header);
                        ScriptMap[diag.SafeFileName] = textToSave;
                        ScriptPathMap[diag.SafeFileName] = diag.FileName;

                        tab.Header = diag.SafeFileName;
                        tab.FilePath = diag.FileName;
                        File.WriteAllText(diag.FileName, textToSave);
                    }
                }
                else
                {
                    File.WriteAllText(tab.FilePath, textToSave);
                }
            });
        }

        public bool OpenScriptsFromXML()
        {
            var storedScripts = TabSaver.LoadFromXML();
            if (storedScripts?.Scripts == null || storedScripts.Scripts.Count == 0)
                return true;

            int index = 0;
            foreach (var script in storedScripts.Scripts)
            {
                if (script == null || string.IsNullOrWhiteSpace(script.Filename))
                    continue;

                var tab = ScriptsPanel.AddScript(script.Filename, script.Path ?? string.Empty, true);
                if (tab == null)
                    continue;

                ScriptMap[script.Filename] = script.Contents ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(script.Path))
                    ScriptPathMap[script.Filename] = script.Path;

                if (index == storedScripts.DefaultIndex)
                    SetText(script.Contents ?? string.Empty);

                index++;
            }

            if (ScriptsPanel.Items.Count > 0)
            {
                int safeIndex = storedScripts.DefaultIndex;
                if (safeIndex < 0 || safeIndex >= ScriptsPanel.Items.Count)
                    safeIndex = 0;

                ScriptsPanel.SelectedIndex = safeIndex;
                var selectedTab = ScriptsPanel.SelectedTab;
                if (selectedTab != null && ScriptMap.TryGetValue(selectedTab.Header, out string contents))
                    SetText(contents);
            }

            ScriptsPanel.DefaultIndex = ScriptsPanel.Items.Count;
            return false;
        }

        private void ScriptTabChanged(object sender, ScriptChangedEventArgs e)
        {
            var previousTab = ScriptsPanel.LastItem;
            if (previousTab != null && !string.IsNullOrWhiteSpace(previousTab.Header))
                ScriptMap[previousTab.Header] = GetText();

            if (ScriptMap.TryGetValue(e.File, out string contents))
            {
                SetText(contents);
            }
        }

        private void ScriptTabClosed(object sender, ScriptChangedEventArgs e)
        {
            ScriptMap.Remove(e.File);
            ScriptPathMap.Remove(e.File);
        }

        private void ScriptTabAdded(object sender, ScriptChangedEventArgs e)
        {
            if (!ScriptMap.ContainsKey(e.File))
            {
                ScriptMap.Add(e.File, "");
                SetText("");
            }
        }
    }
}
