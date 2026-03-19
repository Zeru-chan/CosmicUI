using System.ComponentModel;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using SynapseUI.Types;

namespace SynapseUI.Settings
{
    public class Settings
    {
        private static string defaultFilename = "settings.xml";
        private static string defaultPath = Path.Combine(App.CURRENT_DIR, @"bin\custom", defaultFilename);

        public static AppSettings Load()
        {
            if (!File.Exists(defaultPath))
            {
                var empty = new AppSettings();
                Save(empty);

                return empty;
            }

            var serializer = new XmlSerializer(typeof(AppSettings));

            try
            {
                AppSettings settings;
                using (var fs = new FileStream(defaultPath, FileMode.Open))
                {
                    settings = (AppSettings)serializer.Deserialize(fs);
                }

                return settings;
            }
            catch
            {
                var empty = new AppSettings();
                Save(empty);
                return empty;
            }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(defaultPath));
            var serializer = new XmlSerializer(typeof(AppSettings));
            using (TextWriter writer = new StreamWriter(defaultPath))
            {
                serializer.Serialize(writer, settings);
            }
        }
    }

    [XmlRoot("AppSettings", IsNullable = false)]
    public class AppSettings : INotifyPropertyChanged
    {
        private bool roundedCorners = false;
        private bool unlockFps;
        private bool clearConfirmation = true;
        private bool closeConfirmation = true;
        private bool topMost;
        private string aceTheme = "Twilight";

        [XmlElement("RoundedCorners")]
        public bool RoundedCorners
        {
            get => roundedCorners;
            set
            {
                roundedCorners = value;
                RoundedValue = value ? 15 : 0;
            }
        }

        [XmlElement("UnlockFPS")]
        public bool UnlockFPS
        {
            get => unlockFps;
            set => unlockFps = value;
        }

        [XmlElement("ClearConfirmation")]
        public bool ClearConfirmation
        {
            get => clearConfirmation;
            set => clearConfirmation = value;
        }

        [XmlElement("CloseConfirmation")]
        public bool CloseConfirmation
        {
            get => closeConfirmation;
            set => closeConfirmation = value;
        }

        [XmlElement("TopMost")]
        public bool TopMost
        {
            get => topMost;
            set => topMost = value;
        }

        [XmlElement("AceTheme")]
        public string AceTheme
        {
            get => string.IsNullOrWhiteSpace(aceTheme) ? "Twilight" : aceTheme;
            set => aceTheme = string.IsNullOrWhiteSpace(value) ? "Twilight" : value;
        }

        private int roundedValue = 0;

        [XmlIgnore]
        public int RoundedValue
        {
            get => roundedValue;
            set
            {
                roundedValue = value;
                OnPropertyChanged("RoundedValue");
            }
        }

        public AppSettings() { }

        public void Load()
        {
            var settings = Settings.Load();
            RoundedCorners = settings.RoundedCorners;
            UnlockFPS = settings.UnlockFPS;
            ClearConfirmation = settings.ClearConfirmation;
            CloseConfirmation = settings.CloseConfirmation;
            TopMost = settings.TopMost;
            AceTheme = settings.AceTheme;
        }

        public void Save()
        {
            Settings.Save(this);
        }

        public void ApplyTo(Options options)
        {
            if (options == null)
                return;

            options.UnlockFPS = UnlockFPS;
            options.ClearConfirmation = ClearConfirmation;
            options.CloseConfirmation = CloseConfirmation;
            options.TopMost = TopMost;
        }

        public void CaptureFrom(Options options)
        {
            if (options == null)
                return;

            UnlockFPS = options.UnlockFPS;
            ClearConfirmation = options.ClearConfirmation;
            CloseConfirmation = options.CloseConfirmation;
            TopMost = options.TopMost;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
