using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using D2RSO.Classes;
using D2RSO.Classes.Data;

namespace D2RSO
{
    /// <summary>
    /// Interaction logic for CountersWindow.xaml
    /// </summary>
    public partial class CountersWindow : INotifyPropertyChanged
    {
        private bool _isPreview;
        public ObservableCollection<TrackerItem> SkillTrackerItems { get; } = new();

        public bool IsPreview
        {
            get => _isPreview;
            private set { _isPreview = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotPreview)); }
        }

        public bool IsNotPreview => !IsPreview;


        public CountersWindow()
        {
            InitializeComponent();
            DataContext = this;

            Border.MouseDown += (_, e) =>
            {
                if (e.ChangedButton != MouseButton.Left || IsNotPreview)
                    return;
                DragMove();
            };
            Top = App.Settings.TrackerY;
            Left = App.Settings.TrackerX;
            LocationChanged += (_, _) =>
            {
                App.Settings.TrackerX = (int)Left;
                App.Settings.TrackerY = (int)Top;
                App.Settings.Save();
            };

        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (!_isPreview)
            {
                var windowHwnd = new WindowInteropHelper(this).Handle;
                WindowsServices.SetWindowExTransparent(windowHwnd);
            }
        }



        public void RemTrackerItem(int id)
        {
            this.Dispatcher.Invoke(() =>
            {
                var item = SkillTrackerItems.FirstOrDefault(a => a.Data.Id == id);
                if (item != null)
                    SkillTrackerItems.Remove(item);
            });
        }

        public void AddTrackerItem(SkillDataItem item)
        {
            var old = SkillTrackerItems.FirstOrDefault(a => a.Data.Id == item.Id);
            if (old != null)
            {
                // 기존 타이머 및 Stopwatch, 잔여시간, 빨간색 오버레이 상태 초기화
                old.Reset();
                return;
            }
            // create new tracker
            var tr = new TrackerItem(item);
            tr.OnCompleted += RemTrackerItem;
            this.Dispatcher.Invoke(() =>
            {
                if (App.Settings.IsTrackerInsertToLeft)
                    SkillTrackerItems.Insert(0, tr);
                else SkillTrackerItems.Add(tr);
            });
        }

        public void SetPreview(bool isPreview)
        {
            IsPreview = isPreview;
            Border.IsHitTestVisible = isPreview;
        }

        /// <summary>
        /// Immediately expires all currently running skill timers - sets them to 0.0s
        /// and removes them, as if their cooldown just finished (ESC hotkey).
        /// </summary>
        public void ForceExpireAllTrackers()
        {
            this.Dispatcher.Invoke(() =>
            {
                foreach (var item in SkillTrackerItems.ToList())
                {
                    item.Stop();
                }
            });
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    public class TrackerItem: INotifyPropertyChanged
    {
        private double _currentTimeValue;
        private readonly Timer _timer;
        private bool _isRedOverlayVisible;

        public bool IsRedOverlayVisible
        {
            get => _isRedOverlayVisible;
            set { _isRedOverlayVisible = value; OnPropertyChanged(); }
        }

        public SkillDataItem Data { get; set; }

        public event Action<int> OnCompleted;

        public double CurrentTimeValue
        {
            get => _currentTimeValue;
            set { _currentTimeValue = value;  OnPropertyChanged(); }
        }

        // Tick interval in ms - 100ms gives 0.1s resolution for sub-second (decimal) durations
        private const int TickIntervalMs = 100;
        private readonly System.Diagnostics.Stopwatch _stopwatch = new();

        public TrackerItem(SkillDataItem data)
        {
            Data = data;
            CurrentTimeValue = Data.TimeLength;
            _stopwatch.Restart();
            _timer = new Timer(TickIntervalMs) { AutoReset = true };
            _timer.Elapsed += (_, _) =>
            {
                // Derive remaining time from actual elapsed time rather than
                // naively subtracting the tick interval, so drift doesn't
                // accumulate and short/decimal durations still land on ~0.
                var remaining = Math.Round(Data.TimeLength - _stopwatch.Elapsed.TotalSeconds, 1, MidpointRounding.AwayFromZero);
                if (remaining < 0)
                    remaining = 0;

                if (App.Settings.IsRedTrackerOverlayEnabled)
                    IsRedOverlayVisible = remaining <= App.Settings.RedTrackerOverlaySec;

                CurrentTimeValue = remaining;

                if (remaining <= 0)
                {
                    Stop();
                }
            };
            _timer.Start();
        }

        public void Reset()
        {
            _stopwatch.Restart();
            CurrentTimeValue = Data.TimeLength;
            if (App.Settings.IsRedTrackerOverlayEnabled)
                IsRedOverlayVisible = CurrentTimeValue <= App.Settings.RedTrackerOverlaySec;
            else
                IsRedOverlayVisible = false;

            if (!_timer.Enabled)
            {
                _timer.Start();
            }
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _currentTimeValue = 0;

            if (App.Settings.PlaySoundOnComplete)
                CompletionSoundPlayer.Play();

            OnCompleted?.Invoke(Data.Id);
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}