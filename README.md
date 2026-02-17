# SO Event System

Designer-friendly, singleton-free event system built on ScriptableObjects for Unity.

## Installation

**Unity Package Manager → Add package from git URL:**

```
https://github.com/BasilSkyWalk/unity-so-events.git
```

Or with a specific version:

```
https://github.com/BasilSkyWalk/unity-so-events.git#1.0.0
```

## Quick Start

### 1. Create an event asset

`Tools > SO Events > Event Creator` → Pick type, name it, click Create.

Or right-click in Project: `Create > SO Events > Game Event (Void)`

### 2. Raise the event

```csharp
public class Player : MonoBehaviour
{
    [SerializeField] private GameEvent onPlayerDeath;

    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0)
            onPlayerDeath.Raise();
    }
}
```

### 3. Listen (no code)

1. Add `Game Event Listener` component to any GameObject
2. Drag the event asset into the Event slot
3. Wire response in the UnityEvent field

Done. The listener responds when the event fires.

## Event Types

| Type | Class | Use Case |
|------|-------|----------|
| Void | `GameEvent` | Signals with no data (death, restart) |
| Int | `IntEvent` | Score, counts |
| Float | `FloatEvent` | Health, timers |
| Bool | `BoolEvent` | Toggles |
| String | `StringEvent` | Messages, IDs |
| Vector2/3 | `Vector2Event`, `Vector3Event` | Positions, directions |

Typed events pass data: `onScored.Raise(score);`

## Non-MonoBehaviour Support

Plain C# classes can subscribe via code:

```csharp
_onPlayerDeath.Subscribe(HandleDeath);
_onPlayerDeath.Unsubscribe(HandleDeath); // Must call manually
```

## License

MIT