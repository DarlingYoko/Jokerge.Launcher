using Gml.Launcher.Core.Mods;

namespace Gml.Launcher.Tests.Mods;

/// <summary>
///     The panel can say which optional mods an optional mod needs. Turning a mod on turns its dependencies on,
///     and a dependency stays locked on while a mod that needs it is on.
/// </summary>
public class OptionalModGraphTests
{
    // a needs b, b needs c; d is on its own; a also lists a mod the profile no longer has.
    private static OptionalModGraph Graph() => new(
        ["a.jar", "b.jar", "c.jar", "d.jar"],
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["a.jar"] = ["b.jar", "gone.jar"],
            ["b.jar"] = ["c.jar"]
        });

    [Test]
    public void AllDependenciesOf_FollowsTheChainAndSkipsMissingMods()
    {
        Assert.That(Graph().AllDependenciesOf("a.jar"), Is.EquivalentTo(new[] { "b.jar", "c.jar" }));
    }

    [Test]
    public void AllDependenciesOf_SurvivesALoop()
    {
        var graph = new OptionalModGraph(["a.jar", "b.jar"], new Dictionary<string, IReadOnlyList<string>>
        {
            ["a.jar"] = ["b.jar"],
            ["b.jar"] = ["a.jar"]
        });

        Assert.That(graph.AllDependenciesOf("a.jar"), Is.EquivalentTo(new[] { "b.jar" }));
    }

    [Test]
    public void Tree_NestsDependencies()
    {
        var tree = Graph().Tree("a.jar");

        Assert.That(tree.Select(n => (n.Key, n.Depth)), Is.EqualTo(new[] { ("b.jar", 0), ("c.jar", 1) }));
    }

    [Test]
    public void Tree_StopsAtALoop()
    {
        var graph = new OptionalModGraph(["a.jar", "b.jar"], new Dictionary<string, IReadOnlyList<string>>
        {
            ["a.jar"] = ["b.jar"],
            ["b.jar"] = ["a.jar"]
        });

        Assert.That(graph.Tree("a.jar").Select(n => n.Key), Is.EqualTo(new[] { "b.jar" }));
    }

    [Test]
    public void Tree_ShowsASharedDependencyOnce()
    {
        // a needs b and c, b also needs c.
        var graph = new OptionalModGraph(["a.jar", "b.jar", "c.jar"], new Dictionary<string, IReadOnlyList<string>>
        {
            ["a.jar"] = ["b.jar", "c.jar"],
            ["b.jar"] = ["c.jar"]
        });

        Assert.That(graph.Tree("a.jar").Select(n => (n.Key, n.Depth)), Is.EqualTo(new[] { ("b.jar", 0), ("c.jar", 1) }));
    }

    [Test]
    public void TopLevel_HidesModsThatAreShownInsideTheModThatNeedsThem()
    {
        Assert.That(Graph().TopLevel(["a.jar", "b.jar", "c.jar", "d.jar"]), Is.EqualTo(new[] { "a.jar", "d.jar" }));
    }

    [Test]
    public void TopLevel_KeepsModsThatOnlyNeedEachOther()
    {
        // A loop saved before the panel checked for it: nobody is on top, but both must stay visible.
        var graph = new OptionalModGraph(["a.jar", "b.jar", "d.jar"], new Dictionary<string, IReadOnlyList<string>>
        {
            ["a.jar"] = ["b.jar"],
            ["b.jar"] = ["a.jar"]
        });

        Assert.That(graph.TopLevel(["a.jar", "b.jar", "d.jar"]), Is.EqualTo(new[] { "a.jar", "d.jar" }));
    }

    [Test]
    public void NeededBy_ListsEnabledModsThatNeedIt()
    {
        var graph = Graph();

        Assert.Multiple(() =>
        {
            Assert.That(graph.NeededBy("c.jar", ["a.jar", "b.jar", "c.jar"]),
                Is.EquivalentTo(new[] { "a.jar", "b.jar" }));
            Assert.That(graph.NeededBy("c.jar", ["c.jar", "d.jar"]), Is.Empty);
        });
    }

    [Test]
    public void Enable_TurnsOnTheWholeChainAndMarksItAutomatic()
    {
        var change = OptionalModToggle.Enable(Graph(), enabled: ["c.jar"], automatic: [], "a.jar");

        Assert.Multiple(() =>
        {
            Assert.That(change.ToEnable, Is.EquivalentTo(new[] { "a.jar", "b.jar" }));
            Assert.That(change.ToDisable, Is.Empty);
            // c was already on by the player's choice, so only b counts as turned on automatically.
            Assert.That(change.Automatic, Is.EquivalentTo(new[] { "b.jar" }));
        });
    }

    [Test]
    public void Enable_ByThePlayer_MakesAnAutomaticModTheirChoice()
    {
        var change = OptionalModToggle.Enable(Graph(), enabled: ["b.jar", "c.jar"], automatic: ["b.jar", "c.jar"],
            "b.jar");

        // b is now the player's own; c is still on only because b needs it.
        Assert.That(change.Automatic, Is.EquivalentTo(new[] { "c.jar" }));
    }

    [Test]
    public void Disable_IsRefusedWhileAnotherEnabledModNeedsIt()
    {
        var change = OptionalModToggle.Disable(Graph(), enabled: ["a.jar", "b.jar", "c.jar"], automatic: ["b.jar", "c.jar"],
            "c.jar");

        Assert.That(change, Is.Null);
    }

    [Test]
    public void Disable_AlsoTurnsOffDependenciesThatWereTurnedOnAutomatically()
    {
        var change = OptionalModToggle.Disable(Graph(), enabled: ["a.jar", "b.jar", "c.jar", "d.jar"],
            automatic: ["b.jar", "c.jar"], "a.jar");

        Assert.Multiple(() =>
        {
            Assert.That(change!.ToDisable, Is.EquivalentTo(new[] { "a.jar", "b.jar", "c.jar" }));
            Assert.That(change.ToEnable, Is.Empty);
            Assert.That(change.Automatic, Is.Empty);
        });
    }

    [Test]
    public void Disable_KeepsDependenciesThePlayerTurnedOnThemselves()
    {
        var change = OptionalModToggle.Disable(Graph(), enabled: ["a.jar", "b.jar", "c.jar"], automatic: ["b.jar"],
            "a.jar");

        Assert.That(change!.ToDisable, Is.EquivalentTo(new[] { "a.jar", "b.jar" }));
    }

    [Test]
    public void Disable_KeepsAnAutomaticDependencyStillNeededByAnotherMod()
    {
        var graph = new OptionalModGraph(["a.jar", "e.jar", "c.jar"], new Dictionary<string, IReadOnlyList<string>>
        {
            ["a.jar"] = ["c.jar"],
            ["e.jar"] = ["c.jar"]
        });

        var change = OptionalModToggle.Disable(graph, enabled: ["a.jar", "e.jar", "c.jar"], automatic: ["c.jar"],
            "a.jar");

        Assert.Multiple(() =>
        {
            Assert.That(change!.ToDisable, Is.EquivalentTo(new[] { "a.jar" }));
            Assert.That(change.Automatic, Is.EquivalentTo(new[] { "c.jar" }));
        });
    }

    [Test]
    public void Repair_TurnsOnDependenciesAddedAfterTheModWasEnabled()
    {
        // The admin gave a a dependency after the player had a on, or the player renamed the file by hand.
        var change = OptionalModToggle.Repair(Graph(), enabled: ["a.jar", "d.jar"], automatic: ["gone.jar"]);

        Assert.Multiple(() =>
        {
            Assert.That(change.ToEnable, Is.EquivalentTo(new[] { "b.jar", "c.jar" }));
            Assert.That(change.ToDisable, Is.Empty);
            Assert.That(change.Automatic, Is.EquivalentTo(new[] { "b.jar", "c.jar" }));
        });
    }

    [Test]
    public void Repair_NothingToDo_ChangesNothing()
    {
        var change = OptionalModToggle.Repair(Graph(), enabled: ["d.jar"], automatic: []);

        Assert.That(change.IsEmpty, Is.True);
    }
}
