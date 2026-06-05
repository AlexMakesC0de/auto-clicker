using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AutoClicker.App.Tray;
using AutoClicker.Core;
using Avalonia.Threading;

namespace AutoClicker.App.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly TrayAppController _controller;

    private TargetMode _mode;
    private int _intervalMs;
    private ClickButton _button;
    private ClickType _clickType;
    private bool _jitterEnabled;
    private bool _hasClickLimit;
    private int _clickLimit;
    private string _startStopText = "";
    private string _captureText = "";
    private bool _recordingStartStop;
    private bool _recordingCapture;
    private string _status = "";

    private Hotkey _startStopHotkey;
    private Hotkey _captureHotkey;

    public SettingsViewModel(TrayAppController controller)
    {
        _controller = controller;
        LoadFrom(controller.Settings);
        controller.SettingsChangedExternally += (_, _) =>
            Dispatcher.UIThread.Post(() => LoadFrom(controller.Settings));
    }

    private void LoadFrom(AppSettings s)
    {
        Mode = s.TargetMode;
        _intervalMs = s.IntervalMs; OnChanged(nameof(IntervalMs));
        _button = s.Button; OnChanged(nameof(Button));
        _clickType = s.ClickType; OnChanged(nameof(ClickType));
        _jitterEnabled = s.JitterEnabled; OnChanged(nameof(JitterEnabled));
        _hasClickLimit = s.ClickLimit is not null; OnChanged(nameof(HasClickLimit));
        _clickLimit = s.ClickLimit ?? 100; OnChanged(nameof(ClickLimit));
        _startStopHotkey = s.StartStopHotkey;
        _captureHotkey = s.CaptureHotkey;
        StartStopText = s.StartStopHotkey.ToDisplayString();
        CaptureText = s.CaptureHotkey.ToDisplayString();

        Steps.Clear();
        foreach (var step in s.Sequence) Steps.Add(new SequenceStepViewModel(step));
    }

    public IReadOnlyList<TargetMode> Modes { get; } =
        new[] { TargetMode.CurrentCursor, TargetMode.Sequence };
    public IReadOnlyList<ClickButton> Buttons { get; } =
        new[] { ClickButton.Left, ClickButton.Middle, ClickButton.Right };
    public IReadOnlyList<ClickType> ClickTypes { get; } =
        new[] { ClickType.Single, ClickType.Double };

    public ObservableCollection<SequenceStepViewModel> Steps { get; } = new();

    public TargetMode Mode
    {
        get => _mode;
        set
        {
            if (Set(ref _mode, value))
            {
                OnChanged(nameof(IsCursorMode));
                OnChanged(nameof(IsSequenceMode));
            }
        }
    }

    public bool IsCursorMode => Mode == TargetMode.CurrentCursor;
    public bool IsSequenceMode => Mode == TargetMode.Sequence;

    public int IntervalMs { get => _intervalMs; set => Set(ref _intervalMs, value); }
    public ClickButton Button { get => _button; set => Set(ref _button, value); }
    public ClickType ClickType { get => _clickType; set => Set(ref _clickType, value); }
    public bool JitterEnabled { get => _jitterEnabled; set => Set(ref _jitterEnabled, value); }
    public bool HasClickLimit { get => _hasClickLimit; set => Set(ref _hasClickLimit, value); }
    public int ClickLimit { get => _clickLimit; set => Set(ref _clickLimit, value); }

    public string StartStopText { get => _startStopText; set => Set(ref _startStopText, value); }
    public string CaptureText { get => _captureText; set => Set(ref _captureText, value); }
    public bool RecordingStartStop { get => _recordingStartStop; set => Set(ref _recordingStartStop, value); }
    public bool RecordingCapture { get => _recordingCapture; set => Set(ref _recordingCapture, value); }
    public string Status { get => _status; set => Set(ref _status, value); }

    public void AddBlankStep() => Steps.Add(new SequenceStepViewModel(new SequenceStep()));
    public void ClearSteps() => Steps.Clear();
    public void RemoveStep(SequenceStepViewModel row) => Steps.Remove(row);

    public void MoveStepUp(SequenceStepViewModel row)
    {
        int i = Steps.IndexOf(row);
        if (i > 0) Steps.Move(i, i - 1);
    }

    public void MoveStepDown(SequenceStepViewModel row)
    {
        int i = Steps.IndexOf(row);
        if (i >= 0 && i < Steps.Count - 1) Steps.Move(i, i + 1);
    }

    public void RecordStartStop()
    {
        RecordingStartStop = true;
        StartStopText = "Press keys… (Esc to cancel)";
        _controller.BeginRecordHotkey(hk => Dispatcher.UIThread.Post(() => OnRecorded(hk, isStartStop: true)));
    }

    public void RecordCapture()
    {
        RecordingCapture = true;
        CaptureText = "Press keys… (Esc to cancel)";
        _controller.BeginRecordHotkey(hk => Dispatcher.UIThread.Post(() => OnRecorded(hk, isStartStop: false)));
    }

    private void OnRecorded(Hotkey hk, bool isStartStop)
    {
        RecordingStartStop = false;
        RecordingCapture = false;

        var other = isStartStop ? _captureHotkey : _startStopHotkey;
        if (hk == other)
        {
            Status = $"{hk.ToDisplayString()} is already used by the other hotkey.";
            StartStopText = _startStopHotkey.ToDisplayString();
            CaptureText = _captureHotkey.ToDisplayString();
            return;
        }

        if (isStartStop) { _startStopHotkey = hk; StartStopText = hk.ToDisplayString(); }
        else { _captureHotkey = hk; CaptureText = hk.ToDisplayString(); }
        Status = "Hotkey set (remember to Save).";
    }

    public void Save()
    {
        var updated = new AppSettings
        {
            IntervalMs = Math.Max(1, IntervalMs),
            Button = Button,
            ClickType = ClickType,
            ClickLimit = HasClickLimit ? Math.Max(1, ClickLimit) : null,
            JitterEnabled = JitterEnabled,
            TargetMode = Mode,
            Sequence = Steps.Select(r => r.ToModel()).ToList(),
            StartStopHotkey = _startStopHotkey,
            CaptureHotkey = _captureHotkey,
        };
        _controller.ApplySettings(updated);
        Status = "Saved.";
    }
}
