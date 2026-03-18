using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SynapseUI.Types
{
    public class Options : INotifyPropertyChanged
    {
        public bool ClearConfirmation { get; set; } = true;
        public bool CloseConfirmation { get; set; } = true;
        public bool TopMost
        {
            get => _topMost;
            set
            {
                _topMost = value;
                OnPropertyChanged(nameof(TopMost));
            }
        }

        public bool UnlockFPS { get; set; }

        private bool _topMost;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void SetProperty(string name, bool value)
        {
            var propInfo = GetType().GetProperty(name);
            propInfo?.SetValue(this, value);
        }

        public bool GetProperty(string name)
        {
            var propInfo = GetType().GetProperty(name);
            return propInfo != null && (bool)propInfo.GetValue(this);
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
            Add(new OptionEntry("Unlock FPS", nameof(Options.UnlockFPS)));
            Add(new OptionEntry("Clear Editor Prompt", nameof(Options.ClearConfirmation)));
            Add(new OptionEntry("File Closing Prompt", nameof(Options.CloseConfirmation)));
            Add(new OptionEntry("Top Most", nameof(Options.TopMost)));
        }
    }
}
