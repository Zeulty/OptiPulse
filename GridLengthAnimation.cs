using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace ChlorideTweaks
{
    /// <summary>
    /// Animates a GridLength (a column's Width or a row's Height) between
    /// pixel values - WPF has no built-in GridLength animation, which is what
    /// the collapsible sidebar needs for its smooth 220px <-> 64px resize.
    /// </summary>
    public sealed class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(nameof(From), typeof(GridLength),
                typeof(GridLengthAnimation), new PropertyMetadata(new GridLength(220D)));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(nameof(To), typeof(GridLength),
                typeof(GridLengthAnimation), new PropertyMetadata(new GridLength(220D)));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        /// <summary>Optional easing applied to the animation progress
        /// (custom animations do not inherit Timeline.EasingFunction).</summary>
        public IEasingFunction? Easing { get; set; }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue,
            AnimationClock animationClock)
        {
            if (animationClock.CurrentProgress is not { } rawProgress)
                return To;

            double progress = Easing?.Ease(rawProgress) ?? rawProgress;
            double from = From.Value;
            double to = To.Value;
            return new GridLength(from + (to - from) * progress, GridUnitType.Pixel);
        }
    }
}
