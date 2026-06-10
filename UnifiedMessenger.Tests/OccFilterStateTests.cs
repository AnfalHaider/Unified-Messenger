using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OccFilterStateTests
{
    [Fact]
    public void SettingBranchKey_RaisesChanged()
    {
        var state = OccFilterState.CreateForTests();
        var changed = false;
        state.Changed += (_, _) => changed = true;

        state.BranchKey = "F-11";

        Assert.Equal("F-11", state.BranchKey);
        Assert.True(changed);
    }

    [Fact]
    public void SettingSameBranchKey_DoesNotRaiseChanged()
    {
        var state = OccFilterState.CreateForTests();
        state.BranchKey = "F-11";

        var changeCount = 0;
        state.Changed += (_, _) => changeCount++;

        state.BranchKey = "f-11";

        Assert.Equal(0, changeCount);
    }

    [Fact]
    public void Clear_RemovesBranchKeyAndRaisesChanged()
    {
        var state = OccFilterState.CreateForTests();
        state.BranchKey = "Sales";

        var changed = false;
        state.Changed += (_, _) => changed = true;

        state.Clear();

        Assert.Null(state.BranchKey);
        Assert.True(changed);
    }
}
