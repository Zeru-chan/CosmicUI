using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SynapseUI.Types
{
    /// <summary>
    /// A custom implementaion of how Synapse stores and handles options, it's practically the same apart from it includes { get; set; } accessors,
    /// which allow the property to be detected by object.GetProperty().
    /// </summary>
    public class Options : INotifyPropertyChanged
    {
        private bool autoAttach;
        public bool AutoAttach
        {
            get { return autoAttach; }
            set { autoAttach = value; }
        }

        private bool autoJoin;
        public bool AutoJoin // Unused by sxlib.
        {
            get { return autoJoin; }
            set { autoJoin = value; }
        }

        private bool autoLaunch;
        public bool AutoLaunch
        {
            get { return autoLaunch; }
            set { autoLaunch = value; }
        }

        private bool clearConfirmation;
        public bool ClearConfirmation
        {
            get { return clearConfirmation; }
            set { clearConfirmation = value; }
        }

        private bool closeConfirmation;
        public bool CloseConfirmation
        {
            get { return closeConfirmation; }
            set { closeConfirmation = value; }
        }

        private bool internalU;
        public bool InternalUI
        {
            get { return internalU; }
            set { internalU = value; }
        }

        private bool topMost;
        public bool TopMost
        {
            get { return topMost; }
            set
            {
                topMost = value;
                OnPropertyChanged("TopMost");
            }
        }

        private bool unlockFPS;
        public bool UnlockFPS
        {
            get { return unlockFPS; }
            set { unlockFPS = value; }
        }

        private bool silentLaunch;
        public bool SilentLaunch
        {
            get { return silentLaunch; }
            set { silentLaunch = value; }
        }

        public Options() : base() { }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void SetProperty(string name, bool value)
        {
            var propInfo = this.GetType().GetProperty(name);
            propInfo.SetValue(this, value);
        }

        public bool GetProperty(string name)
        {
            var propInfo = this.GetType().GetProperty(name);
            return (bool)propInfo.GetValue(this);
        }
    }

    public class OptionEntry
    {
        public string FriendlyName { get; set; }
        public string Name { get; set; }

        public OptionEntry(string friendlyName, string name)
        {
            FriendlyName = friendlyName;
            Name = name;
        }
    }

    public class OptionsEntryList : ObservableCollection<OptionEntry>
    {
        public OptionsEntryList() : base()
        {
            Add(new OptionEntry("Unlock FPS", "UnlockFPS"));
            Add(new OptionEntry("Auto-Launch", "AutoLaunch"));
            Add(new OptionEntry("Auto-Attach", "AutoAttach"));
            Add(new OptionEntry("Silent Launch", "SilentLaunch"));
            Add(new OptionEntry("Clear Editor Prompt", "ClearConfirmation"));
            Add(new OptionEntry("File Closing Prompt", "CloseConfirmation"));
            Add(new OptionEntry("Internal UI", "InternalUI"));
            Add(new OptionEntry("Top Most", "TopMost"));
        }
    }
}
