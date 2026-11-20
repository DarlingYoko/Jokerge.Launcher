using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Gml.Launcher.Core.Skin3D;

namespace Gml.Launcher.Views.Components;

/// <summary>
///     Player model with skin and cape, turned with the mouse. Drawn with Avalonia's 2D API: under an orthographic
///     camera every box face is a parallelogram, so each one is a texture drawn through an affine transform
///     (see <see cref="SkinProjector" />). No WebView, GPU context or 3D package is involved.
/// </summary>
public class SkinViewer3D : Control
{
    public static readonly StyledProperty<string?> SkinUrlProperty =
        AvaloniaProperty.Register<SkinViewer3D, string?>(nameof(SkinUrl));

    public static readonly StyledProperty<string?> CapeUrlProperty =
        AvaloniaProperty.Register<SkinViewer3D, string?>(nameof(CapeUrl));

    public static readonly DirectProperty<SkinViewer3D, bool> IsTextureLoadedProperty =
        AvaloniaProperty.RegisterDirect<SkinViewer3D, bool>(nameof(IsTextureLoaded), o => o.IsTextureLoaded);

    private const double DefaultYaw = 0.55;
    private const double DefaultPitch = 0.12;
    private const double MinPitch = -0.5;
    private const double MaxPitch = 0.6;
    private const double DragSpeed = 0.011;
    private const double AutoRotateSpeed = 0.45;
    private static readonly TimeSpan AutoRotateDelay = TimeSpan.FromSeconds(3);

    // Model half-extents (skin px) with some air around it.
    private const double ViewHalfHeight = 18.5;
    private const double ViewHalfWidth = 13;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private CancellationTokenSource? _skinLoad;
    private CancellationTokenSource? _capeLoad;
    private SkinTexture? _skin;
    private SkinTexture? _cape;
    private IReadOnlyList<SkinBox> _model = [];
    private bool _isTextureLoaded;

    private double _yaw = DefaultYaw;
    private double _pitch = DefaultPitch;
    private bool _dragging;
    private Point _lastPointer;
    private TimeSpan _lastInteraction = TimeSpan.FromSeconds(-10);
    private TimeSpan _lastFrame;
    private bool _animating;

    static SkinViewer3D()
    {
        AffectsRender<SkinViewer3D>(SkinUrlProperty, CapeUrlProperty);
        FocusableProperty.OverrideDefaultValue<SkinViewer3D>(false);
    }

    public SkinViewer3D()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    public string? SkinUrl
    {
        get => GetValue(SkinUrlProperty);
        set => SetValue(SkinUrlProperty, value);
    }

    public string? CapeUrl
    {
        get => GetValue(CapeUrlProperty);
        set => SetValue(CapeUrlProperty, value);
    }

    public bool IsTextureLoaded
    {
        get => _isTextureLoaded;
        private set => SetAndRaise(IsTextureLoadedProperty, ref _isTextureLoaded, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SkinUrlProperty)
            LoadTexture(change.GetNewValue<string?>(), ref _skinLoad, texture =>
            {
                _skin = texture;
                IsTextureLoaded = texture is not null;
            });
        else if (change.Property == CapeUrlProperty)
            LoadTexture(change.GetNewValue<string?>(), ref _capeLoad,
                texture => _cape = texture is { HasPixels: true } ? texture : null);
    }

    private void LoadTexture(string? url, ref CancellationTokenSource? pending, Action<SkinTexture?> apply)
    {
        pending?.Cancel();
        var cts = pending = new CancellationTokenSource();

        _ = SkinTextureLoader.LoadAsync(url, cts.Token).ContinueWith(task =>
        {
            var texture = task.IsCompletedSuccessfully ? task.Result : null;
            Dispatcher.UIThread.Post(() =>
            {
                if (cts.IsCancellationRequested)
                    return;

                apply(texture);
                RebuildModel();
            });
        }, TaskScheduler.Default);
    }

    private void RebuildModel()
    {
        _model = _skin is null ? [] : SkinModelBuilder.Build(_skin.Layout, hasCape: _cape is not null);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // Transparent fill makes the whole area grab the mouse, not just the painted pixels.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        if (_skin is null || _model.Count == 0)
            return;

        var scale = Math.Min(Bounds.Height / (2 * ViewHalfHeight), Bounds.Width / (2 * ViewHalfWidth));
        var camera = new SkinCamera(_yaw, _pitch, scale, Bounds.Width / 2, Bounds.Height / 2);
        var pose = SkinPose.Idle(_clock.Elapsed.TotalSeconds);
        // Faces overlap by a fraction of a screen pixel to hide anti-aliased seams between them.
        var overlap = 0.6 / scale;

        foreach (var face in SkinProjector.Project(_model, pose, camera))
        {
            var texture = face.Part == SkinPart.Cape ? _cape : _skin;
            if (texture is null)
                continue;

            var texelScale = texture.Width / 64.0;
            var rect = face.Face.Texture;
            var source = new Rect(rect.X * texelScale, rect.Y * texelScale, rect.Width * texelScale,
                rect.Height * texelScale);
            var destination = new Rect(-overlap, -overlap, rect.Width + 2 * overlap, rect.Height + 2 * overlap);
            var t = face.Transform;

            using (context.PushTransform(new Matrix(t.M11, t.M12, t.M21, t.M22, t.OffsetX, t.OffsetY)))
                context.DrawImage(texture.Bitmap, source, destination);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _animating = true;
        _lastFrame = _clock.Elapsed;
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animating = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void RequestFrame() => TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_ => OnFrame());

    private void OnFrame()
    {
        if (!_animating)
            return;

        var now = _clock.Elapsed;
        var delta = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;

        if (!_dragging && now - _lastInteraction > AutoRotateDelay)
            _yaw = (_yaw + delta * AutoRotateSpeed) % (2 * Math.PI);

        if (_skin is not null)
            InvalidateVisual();
        RequestFrame();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount == 2)
        {
            _yaw = DefaultYaw;
            _pitch = DefaultPitch;
        }

        _dragging = true;
        _lastPointer = e.GetPosition(this);
        _lastInteraction = _clock.Elapsed;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
            return;

        var position = e.GetPosition(this);
        var delta = position - _lastPointer;
        _lastPointer = position;
        _yaw += delta.X * DragSpeed;
        _pitch = Math.Clamp(_pitch + delta.Y * DragSpeed, MinPitch, MaxPitch);
        _lastInteraction = _clock.Elapsed;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;

        _dragging = false;
        _lastInteraction = _clock.Elapsed;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }
}
