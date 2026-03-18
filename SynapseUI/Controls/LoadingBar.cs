using System;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Globalization;
using SynapseUI.Functions.Utils;

namespace SynapseUI.Controls
{
    [TemplatePart(Name = "outterBar", Type = typeof(Border))]
    [TemplatePart(Name = "innerBar", Type = typeof(Border))]
    public class CustomLoadingBar : Control
    {
        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, Math.Min(value, 100)); }
        }

        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
                "Progress", typeof(double), typeof(CustomLoadingBar),
                new PropertyMetadata(0.0, new PropertyChangedCallback(ProgressChangedCallback)));

        public CustomLoadingBar()
        {
            DefaultStyleKey = typeof(CustomLoadingBar);
            Loaded += CustomLoadingBar_Loaded;
        }

        private void CustomLoadingBar_Loaded(object sender, RoutedEventArgs e)
        {
            SetProgress(Progress);

            AnimationStoryboard.Completed += (s, ee) =>
            {
                Locked = false;
                AnimationStoryboard.Stop();
            };
        }

        private static void ProgressChangedCallback(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            CustomLoadingBar loadingBar = (CustomLoadingBar)obj;
            double value = (double)args.NewValue;

            loadingBar.OnProgressChanged(
                new ProgressChangedEventArgs(ProgressChangedEvent, value));
        }

        public static readonly RoutedEvent ProgressChangedEvent =
            EventManager.RegisterRoutedEvent("ProgressChanged", RoutingStrategy.Direct,
                typeof(ProgressChangedEventHandler), typeof(CustomLoadingBar));

        public event ProgressChangedEventHandler ProgressChanged
        {
            add { AddHandler(ProgressChangedEvent, value); }
            remove { RemoveHandler(ProgressChangedEvent, value); }
        }

        protected virtual void OnProgressChanged(ProgressChangedEventArgs e)
        {
            if (InnerBorder != null && !Locked)
                SetProgress(e.Value);

            RaiseEvent(e);
        }

        public Border OutterBorder { get; private set; }
        public Border InnerBorder { get; private set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            OutterBorder = GetTemplateChild("outterBar") as Border;
            InnerBorder = GetTemplateChild("innerBar") as Border;
        }

        public double PercentageParse(double perc)
        {
            if (OutterBorder == null) return 0;

            double baseWidth = OutterBorder.ActualWidth > 0 ? OutterBorder.ActualWidth : OutterBorder.Width;

            if (double.IsNaN(baseWidth) || baseWidth <= 0)
                return 0;

            return Math.Max(0, (baseWidth - 2) * (perc / 100));
        }

        public void SetProgress(double value)
        {
            if (InnerBorder == null) return;

            double newWidth = PercentageParse(value);
            if (!double.IsNaN(newWidth))
            {
                InnerBorder.Width = newWidth;
            }
        }

        public bool Locked { get; private set; } = false;
        public Storyboard AnimationStoryboard { get; private set; } = new Storyboard();
        private readonly TimeSpan Duration = TimeSpan.FromMilliseconds(500);

        private DoubleAnimation CreateAnimation(double value)
        {
            double startWidth = InnerBorder.ActualWidth;
            double endWidth = PercentageParse(value);

            var anim = new DoubleAnimation
            {
                From = double.IsNaN(startWidth) ? 0 : startWidth,
                To = endWidth,
                Duration = Duration,
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(anim, InnerBorder);
            Storyboard.SetTargetProperty(anim, new PropertyPath(WidthProperty));

            return anim;
        }

        public void AnimateProgress(double targetValue)
        {
            if (InnerBorder == null || OutterBorder == null)
                return;

            if (Locked)
            {
                AnimationStoryboard.Stop();
            }

            var anim = CreateAnimation(targetValue);

            AnimationStoryboard.Children.Clear();
            AnimationStoryboard.Children.Add(anim);

            Locked = true;
            AnimationStoryboard.Begin();

            Progress = targetValue;
        }

        public void AnimateFinish()
        {
            AnimateProgress(100);
        }
    }

    public delegate void ProgressChangedEventHandler(object sender, ProgressChangedEventArgs e);

    public class ProgressChangedEventArgs : RoutedEventArgs
    {
        public double Value { get; }
        public ProgressChangedEventArgs(RoutedEvent e, double val)
        {
            Value = val;
            RoutedEvent = e;
        }
    }

    public class InnerBorderValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return 0.0;
            double val = (double)value;
            double param = double.Parse(parameter.ToString(), CultureInfo.InvariantCulture);
            return Math.Max(0, val - param);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
