// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
namespace WlxOverlay.Types;

public class Actions
{
    public List<Action>? actions;
    public List<ActionSet>? action_sets;
}

public class Action
{
    public string? name;
    public string? type;
    public string? requirement;
}

public class ActionSet
{
    public string? name;
    public string? usage;
}