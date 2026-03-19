using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SynapseUI.Controls
{
    [TemplatePart(Name = "presenter", Type = typeof(ContentPresenter))]
    public class DisappearingLabel : Label, INotifyPropertyChanged
    {
        private int _stateVersion;

        public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(1500);

        public bool IsActive
        {
            get { return (bool)GetValue(ActiveProperty); }
            private set
            {
                SetValue(ActiveProperty, value);
                OnPropertyChanged("IsActive");
            }
        }

        public static readonly DependencyProperty ActiveProperty = DependencyProperty.Register(
            "IsActive", typeof(bool), typeof(DisappearingLabel));

        public DisappearingLabel() { }

        public async Task SetActive(bool val)
        {
            if (val)
            {
                Interlocked.Increment(ref _stateVersion);
                IsActive = true;
                return;
            }

            int version = _stateVersion;
            if (!val)
            {
                await Task.Delay(Duration);
                if (version != _stateVersion)
                    return;

                IsActive = false;
                await Task.Delay(TimeSpan.FromMilliseconds(250));
                if (version != _stateVersion)
                    return;

                Content = null;
            }
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            if (!IsActive && Content != null)
                IsActive = true;

            base.OnContentChanged(oldContent, newContent);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
