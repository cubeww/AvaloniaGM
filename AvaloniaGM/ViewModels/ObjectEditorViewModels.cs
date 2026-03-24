using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using AvaloniaGM.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaGM.ViewModels;

public sealed class ResourceReferenceOption<TResource> where TResource : Resource
{
    public string DisplayName { get; }

    public TResource? Resource { get; }

    public ResourceReferenceOption(string displayName, TResource? resource)
    {
        DisplayName = displayName;
        Resource = resource;
    }
}

public partial class ObjectEditorViewModel : ObservableObject
{
    private readonly GameObject _gameObject;
    private readonly Action<Resource> _refreshResourceVisuals;
    private readonly Action<string> _appendOutput;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpritePreview))]
    [NotifyPropertyChangedFor(nameof(HasSpritePreview))]
    [NotifyPropertyChangedFor(nameof(HasNoSpritePreview))]
    private ResourceReferenceOption<Sprite>? spriteOption;

    [ObservableProperty]
    private ResourceReferenceOption<GameObject>? parentOption;

    [ObservableProperty]
    private ResourceReferenceOption<Sprite>? maskOption;

    [ObservableProperty]
    private bool solid;

    [ObservableProperty]
    private bool visible;

    [ObservableProperty]
    private int depth;

    [ObservableProperty]
    private bool persistent;

    [ObservableProperty]
    private bool physicsObject;

    [ObservableProperty]
    private bool physicsObjectSensor;

    [ObservableProperty]
    private int physicsObjectShape;

    [ObservableProperty]
    private float physicsObjectDensity;

    [ObservableProperty]
    private float physicsObjectRestitution;

    [ObservableProperty]
    private int physicsObjectGroup;

    [ObservableProperty]
    private float physicsObjectLinearDamping;

    [ObservableProperty]
    private float physicsObjectAngularDamping;

    [ObservableProperty]
    private float physicsObjectFriction;

    [ObservableProperty]
    private bool physicsObjectAwake;

    [ObservableProperty]
    private bool physicsObjectKinematic;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAddingCollisionEvent))]
    private GameObjectEventType newEventType = GameObjectEventType.Create;

    [ObservableProperty]
    private int newEventNumber;

    [ObservableProperty]
    private ResourceReferenceOption<GameObject>? newCollisionObjectOption;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedEventCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelectedEvent))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedEvent))]
    [NotifyPropertyChangedFor(nameof(SelectedCodeDocument))]
    private ObjectEventItemViewModel? selectedEvent;

    public string Name => _gameObject.Name;

    public ObservableCollection<ResourceReferenceOption<Sprite>> SpriteOptions { get; } = new();

    public ObservableCollection<ResourceReferenceOption<Sprite>> MaskOptions { get; } = new();

    public ObservableCollection<ResourceReferenceOption<GameObject>> ParentOptions { get; } = new();

    public ObservableCollection<ResourceReferenceOption<GameObject>> CollisionObjectOptions { get; } = new();

    public ObservableCollection<ObjectEventItemViewModel> Events { get; } = new();

    public Array EventTypes { get; } = Enum.GetValues<GameObjectEventType>();

    public Bitmap? SpritePreview => SpriteOption?.Resource?.Frames
        .OrderBy(static frame => frame.Index)
        .Select(static frame => frame.Bitmap)
        .FirstOrDefault(static bitmap => bitmap is not null);

    public bool HasSpritePreview => SpritePreview is not null;

    public bool HasNoSpritePreview => SpritePreview is null;

    public bool HasSelectedEvent => SelectedEvent is not null;

    public bool HasNoSelectedEvent => SelectedEvent is null;

    public bool IsAddingCollisionEvent => NewEventType == GameObjectEventType.Collision;

    public IGmlCodeDocument? SelectedCodeDocument => SelectedEvent?.CodeDocument;

    public ObjectEditorViewModel(Project project, GameObject gameObject, Action<Resource> refreshResourceVisuals, Action<string> appendOutput)
    {
        _gameObject = gameObject;
        _refreshResourceVisuals = refreshResourceVisuals;
        _appendOutput = appendOutput;

        PopulateReferenceOptions(project);

        spriteOption = FindOption(SpriteOptions, gameObject.Sprite);
        maskOption = FindOption(MaskOptions, gameObject.Mask);
        parentOption = FindOption(ParentOptions, gameObject.Parent);
        newCollisionObjectOption = CollisionObjectOptions.FirstOrDefault();

        solid = gameObject.Solid;
        visible = gameObject.Visible;
        depth = gameObject.Depth;
        persistent = gameObject.Persistent;
        physicsObject = gameObject.PhysicsObject;
        physicsObjectSensor = gameObject.PhysicsObjectSensor;
        physicsObjectShape = gameObject.PhysicsObjectShape;
        physicsObjectDensity = gameObject.PhysicsObjectDensity;
        physicsObjectRestitution = gameObject.PhysicsObjectRestitution;
        physicsObjectGroup = gameObject.PhysicsObjectGroup;
        physicsObjectLinearDamping = gameObject.PhysicsObjectLinearDamping;
        physicsObjectAngularDamping = gameObject.PhysicsObjectAngularDamping;
        physicsObjectFriction = gameObject.PhysicsObjectFriction;
        physicsObjectAwake = gameObject.PhysicsObjectAwake;
        physicsObjectKinematic = gameObject.PhysicsObjectKinematic;

        RebuildEvents();
    }

    partial void OnSpriteOptionChanged(ResourceReferenceOption<Sprite>? value)
    {
        _gameObject.Sprite = value?.Resource;
        _refreshResourceVisuals(_gameObject);
        OnPropertyChanged(nameof(SpritePreview));
        OnPropertyChanged(nameof(HasSpritePreview));
        OnPropertyChanged(nameof(HasNoSpritePreview));
    }

    partial void OnParentOptionChanged(ResourceReferenceOption<GameObject>? value) => _gameObject.Parent = value?.Resource;

    partial void OnMaskOptionChanged(ResourceReferenceOption<Sprite>? value) => _gameObject.Mask = value?.Resource;

    partial void OnSolidChanged(bool value) => _gameObject.Solid = value;

    partial void OnVisibleChanged(bool value) => _gameObject.Visible = value;

    partial void OnDepthChanged(int value) => _gameObject.Depth = value;

    partial void OnPersistentChanged(bool value) => _gameObject.Persistent = value;

    partial void OnPhysicsObjectChanged(bool value) => _gameObject.PhysicsObject = value;

    partial void OnPhysicsObjectSensorChanged(bool value) => _gameObject.PhysicsObjectSensor = value;

    partial void OnPhysicsObjectShapeChanged(int value) => _gameObject.PhysicsObjectShape = value;

    partial void OnPhysicsObjectDensityChanged(float value) => _gameObject.PhysicsObjectDensity = value;

    partial void OnPhysicsObjectRestitutionChanged(float value) => _gameObject.PhysicsObjectRestitution = value;

    partial void OnPhysicsObjectGroupChanged(int value) => _gameObject.PhysicsObjectGroup = value;

    partial void OnPhysicsObjectLinearDampingChanged(float value) => _gameObject.PhysicsObjectLinearDamping = value;

    partial void OnPhysicsObjectAngularDampingChanged(float value) => _gameObject.PhysicsObjectAngularDamping = value;

    partial void OnPhysicsObjectFrictionChanged(float value) => _gameObject.PhysicsObjectFriction = value;

    partial void OnPhysicsObjectAwakeChanged(bool value) => _gameObject.PhysicsObjectAwake = value;

    partial void OnPhysicsObjectKinematicChanged(bool value) => _gameObject.PhysicsObjectKinematic = value;

    [RelayCommand]
    private void AddEvent()
    {
        if (HasDuplicateNewEvent())
        {
            _appendOutput($"Skipped adding duplicate event to object {Name}.");
            return;
        }

        var gameObjectEvent = new GameObjectEvent
        {
            EventType = NewEventType,
            EventNumber = NewEventNumber,
            CollisionObject = NewEventType == GameObjectEventType.Collision ? NewCollisionObjectOption?.Resource : null,
        };

        _gameObject.Events.Add(gameObjectEvent);

        var eventItem = new ObjectEventItemViewModel(gameObjectEvent, CollisionObjectOptions);
        Events.Add(eventItem);
        SelectedEvent = eventItem;

        _appendOutput($"Added event {eventItem.DisplayName} to object {Name}.");
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedEvent))]
    private void RemoveSelectedEvent()
    {
        if (SelectedEvent is null)
        {
            return;
        }

        var removedName = SelectedEvent.DisplayName;
        _gameObject.Events.Remove(SelectedEvent.GameObjectEvent);
        var removedIndex = Events.IndexOf(SelectedEvent);
        Events.Remove(SelectedEvent);

        SelectedEvent = removedIndex >= 0 && removedIndex < Events.Count
            ? Events[removedIndex]
            : Events.LastOrDefault();

        _appendOutput($"Removed event {removedName} from object {Name}.");
    }

    private bool CanRemoveSelectedEvent() => SelectedEvent is not null;

    private bool HasDuplicateNewEvent()
    {
        return _gameObject.Events.Any(existingEvent =>
            existingEvent.EventType == NewEventType
            && (NewEventType == GameObjectEventType.Collision
                ? ReferenceEquals(existingEvent.CollisionObject, NewCollisionObjectOption?.Resource)
                : existingEvent.EventNumber == NewEventNumber));
    }

    private void PopulateReferenceOptions(Project project)
    {
        SpriteOptions.Clear();
        SpriteOptions.Add(new ResourceReferenceOption<Sprite>("<undefined>", null));
        foreach (var sprite in project.Sprites.OrderBy(static sprite => sprite.Name, StringComparer.OrdinalIgnoreCase))
        {
            SpriteOptions.Add(new ResourceReferenceOption<Sprite>(sprite.Name, sprite));
        }

        MaskOptions.Clear();
        MaskOptions.Add(new ResourceReferenceOption<Sprite>("<undefined>", null));
        foreach (var sprite in project.Sprites.OrderBy(static sprite => sprite.Name, StringComparer.OrdinalIgnoreCase))
        {
            MaskOptions.Add(new ResourceReferenceOption<Sprite>(sprite.Name, sprite));
        }

        ParentOptions.Clear();
        ParentOptions.Add(new ResourceReferenceOption<GameObject>("<undefined>", null));
        CollisionObjectOptions.Clear();
        CollisionObjectOptions.Add(new ResourceReferenceOption<GameObject>("<undefined>", null));

        foreach (var gameObject in project.Objects
                     .Where(objectItem => !ReferenceEquals(objectItem, _gameObject))
                     .OrderBy(static objectItem => objectItem.Name, StringComparer.OrdinalIgnoreCase))
        {
            var option = new ResourceReferenceOption<GameObject>(gameObject.Name, gameObject);
            ParentOptions.Add(option);
            CollisionObjectOptions.Add(option);
        }
    }

    private void RebuildEvents()
    {
        Events.Clear();

        foreach (var gameObjectEvent in _gameObject.Events)
        {
            Events.Add(new ObjectEventItemViewModel(gameObjectEvent, CollisionObjectOptions));
        }

        SelectedEvent = Events.FirstOrDefault();
    }

    private static ResourceReferenceOption<TResource>? FindOption<TResource>(
        IEnumerable<ResourceReferenceOption<TResource>> options,
        TResource? resource)
        where TResource : Resource
    {
        return options.FirstOrDefault(option => ReferenceEquals(option.Resource, resource))
            ?? options.FirstOrDefault(option => option.Resource is null);
    }
}

public partial class ObjectEventItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(IsCollision))]
    [NotifyPropertyChangedFor(nameof(ActionSummary))]
    private GameObjectEventType eventType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private int eventNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private ResourceReferenceOption<GameObject>? collisionObjectOption;

    public GameObjectEvent GameObjectEvent { get; }

    public ObjectEventCodeDocumentViewModel CodeDocument { get; }

    public bool IsCollision => EventType == GameObjectEventType.Collision;

    public string DisplayName => BuildEventDisplayName(EventType, EventNumber, CollisionObjectOption?.DisplayName ?? GameObjectEvent.CollisionObject?.Name);

    public string ActionSummary
    {
        get
        {
            var nonCodeActionCount = GameObjectEvent.Actions.Count(action => !ObjectEventCodeDocumentViewModel.IsCodeAction(action));
            return nonCodeActionCount > 0
                ? $"Code editor active. Preserving {nonCodeActionCount} additional action(s)."
                : "Code editor active.";
        }
    }

    public ObjectEventItemViewModel(GameObjectEvent gameObjectEvent, IEnumerable<ResourceReferenceOption<GameObject>> collisionObjectOptions)
    {
        GameObjectEvent = gameObjectEvent;
        eventType = gameObjectEvent.EventType;
        eventNumber = gameObjectEvent.EventNumber;
        collisionObjectOption = collisionObjectOptions.FirstOrDefault(option => ReferenceEquals(option.Resource, gameObjectEvent.CollisionObject))
            ?? collisionObjectOptions.FirstOrDefault(option => option.Resource is null);
        CodeDocument = new ObjectEventCodeDocumentViewModel(gameObjectEvent, this);
    }

    partial void OnEventTypeChanged(GameObjectEventType value)
    {
        GameObjectEvent.EventType = value;
        if (value != GameObjectEventType.Collision)
        {
            GameObjectEvent.CollisionObject = null;
            CollisionObjectOption = null;
        }
    }

    partial void OnEventNumberChanged(int value)
    {
        GameObjectEvent.EventNumber = value;
    }

    partial void OnCollisionObjectOptionChanged(ResourceReferenceOption<GameObject>? value)
    {
        GameObjectEvent.CollisionObject = value?.Resource;
    }

    internal void NotifyCodeChanged()
    {
        OnPropertyChanged(nameof(ActionSummary));
    }

    private static string BuildEventDisplayName(GameObjectEventType eventType, int eventNumber, string? collisionTarget)
    {
        return eventType switch
        {
            GameObjectEventType.Create => "Create",
            GameObjectEventType.Destroy => "Destroy",
            GameObjectEventType.Alarm => $"Alarm {eventNumber}",
            GameObjectEventType.Step => eventNumber switch
            {
                0 => "Step",
                1 => "Begin Step",
                2 => "End Step",
                _ => $"Step {eventNumber}"
            },
            GameObjectEventType.Collision => $"Collision: {collisionTarget ?? "<undefined>"}",
            GameObjectEventType.Keyboard => $"Keyboard {eventNumber}",
            GameObjectEventType.Mouse => $"Mouse {eventNumber}",
            GameObjectEventType.Other => $"Other {eventNumber}",
            GameObjectEventType.Draw => eventNumber == 0 ? "Draw" : $"Draw {eventNumber}",
            GameObjectEventType.KeyPress => $"Key Press {eventNumber}",
            GameObjectEventType.KeyRelease => $"Key Release {eventNumber}",
            GameObjectEventType.Trigger => $"Trigger {eventNumber}",
            GameObjectEventType.CleanUp => "Clean Up",
            GameObjectEventType.Gesture => $"Gesture {eventNumber}",
            _ => $"{eventType} {eventNumber}"
        };
    }
}

public partial class ObjectEventCodeDocumentViewModel : ObservableObject, IGmlCodeDocument
{
    private readonly GameObjectEvent _gameObjectEvent;
    private readonly ObjectEventItemViewModel _eventItem;

    [ObservableProperty]
    private string sourceCode;

    public ObjectEventCodeDocumentViewModel(GameObjectEvent gameObjectEvent, ObjectEventItemViewModel eventItem)
    {
        _gameObjectEvent = gameObjectEvent;
        _eventItem = eventItem;
        sourceCode = GetCodeFromPrimaryAction(gameObjectEvent.Actions);
    }

    partial void OnSourceCodeChanged(string value)
    {
        var action = GetOrCreatePrimaryCodeAction(_gameObjectEvent.Actions);
        EnsureCodeArgument(action).Value = value;
        action.CodeString = string.Empty;
        _eventItem.NotifyCodeChanged();
    }

    internal static bool IsCodeAction(GameObjectAction action)
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
