using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaGM.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaGM.ViewModels;

public partial class TimelineEditorViewModel : ObservableObject
{
    private readonly Timeline _timeline;
    private readonly Action<string> _appendOutput;
    private bool _isSynchronizingSelectedMomentStep;

    [ObservableProperty]
    private int newMomentStep;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedMomentCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelectedMoment))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedMoment))]
    [NotifyPropertyChangedFor(nameof(SelectedCodeDocument))]
    private TimelineMomentItemViewModel? selectedMoment;

    [ObservableProperty]
    private int selectedMomentStep;

    public string Name => _timeline.Name;

    public ObservableCollection<TimelineMomentItemViewModel> Moments { get; } = new();

    public int MomentCount => Moments.Count;

    public bool HasSelectedMoment => SelectedMoment is not null;

    public bool HasNoSelectedMoment => SelectedMoment is null;

    public IGmlCodeDocument? SelectedCodeDocument => SelectedMoment?.CodeDocument;

    public TimelineEditorViewModel(Timeline timeline, Action<string> appendOutput)
    {
        _timeline = timeline;
        _appendOutput = appendOutput;

        RebuildMoments();
        newMomentStep = ComputeSuggestedMomentStep();
    }

    partial void OnSelectedMomentChanged(TimelineMomentItemViewModel? value)
    {
        _isSynchronizingSelectedMomentStep = true;
        SelectedMomentStep = value?.Step ?? 0;
        _isSynchronizingSelectedMomentStep = false;
    }

    partial void OnSelectedMomentStepChanged(int value)
    {
        if (_isSynchronizingSelectedMomentStep || SelectedMoment is null)
        {
            return;
        }

        if (HasMomentStepConflict(value, SelectedMoment))
        {
            _appendOutput($"Skipped duplicate timeline moment step {value} in {Name}.");

            _isSynchronizingSelectedMomentStep = true;
            SelectedMomentStep = SelectedMoment.Step;
            _isSynchronizingSelectedMomentStep = false;
            return;
        }

        SelectedMoment.Step = value;
        SortMoments(SelectedMoment);
    }

    [RelayCommand]
    private void AddMoment()
    {
        if (HasMomentStepConflict(NewMomentStep, ignoredMoment: null))
        {
            _appendOutput($"Skipped duplicate timeline moment step {NewMomentStep} in {Name}.");
            return;
        }

        var timelineMoment = new TimelineMoment
        {
            Step = NewMomentStep,
        };
        TimelineMomentCodeDocumentViewModel.EnsurePrimaryCodeAction(timelineMoment.Actions);

        _timeline.Moments.Add(timelineMoment);

        var momentItem = new TimelineMomentItemViewModel(timelineMoment);
        Moments.Add(momentItem);
        SortMoments(momentItem);
        SelectedMoment = momentItem;

        OnPropertyChanged(nameof(MomentCount));
        NewMomentStep = ComputeSuggestedMomentStep();

        _appendOutput($"Added timeline moment {momentItem.Step} to {Name}.");
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedMoment))]
    private void RemoveSelectedMoment()
    {
        if (SelectedMoment is null)
        {
            return;
        }

        var removedStep = SelectedMoment.Step;
        _timeline.Moments.Remove(SelectedMoment.TimelineMoment);
        var removedIndex = Moments.IndexOf(SelectedMoment);
        Moments.Remove(SelectedMoment);

        SelectedMoment = removedIndex >= 0 && removedIndex < Moments.Count
            ? Moments[removedIndex]
            : Moments.LastOrDefault();

        OnPropertyChanged(nameof(MomentCount));
        NewMomentStep = ComputeSuggestedMomentStep();

        _appendOutput($"Removed timeline moment {removedStep} from {Name}.");
    }

    private bool CanRemoveSelectedMoment() => SelectedMoment is not null;

    private void RebuildMoments()
    {
        Moments.Clear();

        foreach (var moment in _timeline.Moments.OrderBy(static moment => moment.Step))
        {
            Moments.Add(new TimelineMomentItemViewModel(moment));
        }

        OnPropertyChanged(nameof(MomentCount));
        SelectedMoment = Moments.FirstOrDefault();
    }

    private void SortMoments(TimelineMomentItemViewModel? selectedItem)
    {
        var orderedMoments = _timeline.Moments
            .OrderBy(static moment => moment.Step)
            .ToList();
        _timeline.Moments.Clear();
        _timeline.Moments.AddRange(orderedMoments);

        var orderedItems = Moments
            .OrderBy(static item => item.Step)
            .ToList();
        Moments.Clear();

        foreach (var item in orderedItems)
        {
            Moments.Add(item);
        }

        if (selectedItem is not null)
        {
            SelectedMoment = selectedItem;
        }
    }

    private bool HasMomentStepConflict(int step, TimelineMomentItemViewModel? ignoredMoment)
    {
        return _timeline.Moments.Any(moment =>
            !ReferenceEquals(moment, ignoredMoment?.TimelineMoment)
            && moment.Step == step);
    }

    private int ComputeSuggestedMomentStep()
    {
        return _timeline.Moments.Count == 0
            ? 0
            : _timeline.Moments.Max(static moment => moment.Step) + 1;
    }
}

public partial class TimelineMomentItemViewModel : ObservableObject
{
    private readonly TimelineMoment _timelineMoment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private int step;

    public TimelineMoment TimelineMoment => _timelineMoment;

    public TimelineMomentCodeDocumentViewModel CodeDocument { get; }

    public string DisplayName => $"Step {Step}";

    public TimelineMomentItemViewModel(TimelineMoment timelineMoment)
    {
        _timelineMoment = timelineMoment;
        step = timelineMoment.Step;
        CodeDocument = new TimelineMomentCodeDocumentViewModel(timelineMoment);
    }

    partial void OnStepChanged(int value)
    {
        _timelineMoment.Step = value;
    }
}

public partial class TimelineMomentCodeDocumentViewModel : ObservableObject, IGmlCodeDocument
{
    private readonly TimelineMoment _timelineMoment;

    [ObservableProperty]
    private string sourceCode;

    public TimelineMomentCodeDocumentViewModel(TimelineMoment timelineMoment)
    {
        _timelineMoment = timelineMoment;
        sourceCode = GetCodeFromPrimaryAction(timelineMoment.Actions);
    }

    partial void OnSourceCodeChanged(string value)
    {
        var action = GetOrCreatePrimaryCodeAction(_timelineMoment.Actions);
        EnsureCodeArgument(action).Value = value;
        action.CodeString = string.Empty;
    }

    internal static void EnsurePrimaryCodeAction(ICollection<GameObjectAction> actions)
    {
        _ = GetOrCreatePrimaryCodeAction(actions);
    }

    private static bool IsCodeAction(GameObjectAction action)
    {
        return action.Kind == GameObjectActionKind.Code
            || action.ExecuteType == GameObjectActionExecuteType.Code;
    }

    private static string GetCodeFromPrimaryAction(IEnumerable<GameObjectAction> actions)
    {
        var action = actions.FirstOrDefault(IsCodeAction);
        if (action is null)
        {
            return string.Empty;
        }

        var stringArgument = action.Arguments.FirstOrDefault(argument =>
            argument.Kind == GameObjectActionArgumentKind.String
            || argument.Kind == GameObjectActionArgumentKind.StringExpression);

        return !string.IsNullOrEmpty(stringArgument?.Value)
            ? stringArgument.Value
            : action.CodeString;
    }

    private static GameObjectAction GetOrCreatePrimaryCodeAction(ICollection<GameObjectAction> actions)
    {
        var existingAction = actions.FirstOrDefault(IsCodeAction);
        if (existingAction is not null)
        {
            return existingAction;
        }

        var action = new GameObjectAction
        {
            LibId = 1,
            Id = 603,
            Kind = GameObjectActionKind.Code,
            UseRelative = false,
            IsQuestion = false,
            UseApplyTo = true,
            ExecuteType = GameObjectActionExecuteType.Code,
            FunctionName = string.Empty,
            CodeString = string.Empty,
            WhoName = "self",
            Relative = false,
            IsNot = false,
        };
        action.Arguments.Add(new GameObjectActionArgument
        {
            Kind = GameObjectActionArgumentKind.String,
            Value = string.Empty,
        });
        actions.Add(action);
        return action;
    }

    private static GameObjectActionArgument EnsureCodeArgument(GameObjectAction action)
    {
        var argument = action.Arguments.FirstOrDefault(existingArgument =>
            existingArgument.Kind == GameObjectActionArgumentKind.String
            || existingArgument.Kind == GameObjectActionArgumentKind.StringExpression);

        if (argument is not null)
        {
            return argument;
        }

        argument = new GameObjectActionArgument
        {
            Kind = GameObjectActionArgumentKind.String,
            Value = string.Empty,
        };
        action.Arguments.Insert(0, argument);
        return argument;
    }
}
