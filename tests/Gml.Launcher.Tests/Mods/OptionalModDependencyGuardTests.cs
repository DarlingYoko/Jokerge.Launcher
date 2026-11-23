using System.Reactive.Subjects;
using Gml.Client.Interfaces;
using Gml.Launcher.Core.Mods;
using Gml.Launcher.Tests.Routes;

namespace Gml.Launcher.Tests.Mods;

/// <summary>
///     Right before the game starts the launcher makes sure every enabled optional mod has its dependencies on,
///     even if the player renamed files by hand or the admin added a dependency later.
/// </summary>
public class OptionalModDependencyGuardTests
{
    private string _mods = null!;
    private InMemoryStorage _storage = null!;
    private List<OptionalModDetailsDto>? _details;

    [SetUp]
    public void SetUp()
    {
        _mods = Path.Combine(Path.GetTempPath(), "optional-mods-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mods);
        _storage = new InMemoryStorage();
        _details =
        [
            new OptionalModDetailsDto { Key = "a-optional-mod.jar", Dependencies = ["b-optional-mod.jar"] }
        ];
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_mods, true);

    private void Put(string fileName) => File.WriteAllText(Path.Combine(_mods, fileName), "jar");

    private bool Has(string fileName) => File.Exists(Path.Combine(_mods, fileName));

    private OptionalModDependencyGuard Guard() =>
        new(_ => Task.FromResult<IReadOnlyList<OptionalModDetailsDto>?>(_details), new AutomaticModsStore(_storage));

    [Test]
    public void Files_TellEnabledFromDisabled()
    {
        Put("a-optional-mod.jar");
        Put("b-optional-mod.jar.disabled");
        Put("required.jar");

        var files = new OptionalModFiles(_mods);

        Assert.Multiple(() =>
        {
            Assert.That(files.EnabledKeys(), Is.EquivalentTo(new[] { "a-optional-mod.jar" }));
            Assert.That(files.AllKeys(), Is.EquivalentTo(new[] { "a-optional-mod.jar", "b-optional-mod.jar" }));
        });
    }

    [Test]
    public void Files_ApplyRenamesBothWays_AndSkipsMissingFiles()
    {
        Put("a-optional-mod.jar");
        Put("b-optional-mod.jar.disabled");

        var missing = new OptionalModFiles(_mods).Apply(new OptionalModChange(
            ["b-optional-mod.jar", "gone-optional-mod.jar"], ["a-optional-mod.jar"], []));

        Assert.Multiple(() =>
        {
            Assert.That(Has("a-optional-mod.jar.disabled") && !Has("a-optional-mod.jar"), Is.True);
            Assert.That(Has("b-optional-mod.jar") && !Has("b-optional-mod.jar.disabled"), Is.True);
            Assert.That(missing, Is.EqualTo(new[] { "gone-optional-mod.jar" }));
        });
    }

    [Test]
    public async Task Prepare_TurnsOnMissingDependencies_AndAgainAfterEachSync()
    {
        Put("a-optional-mod.jar");
        Put("b-optional-mod.jar.disabled");
        var synced = new Subject<FileSyncResult>();

        using var _ = await Guard().PrepareAsync(_mods, "Waffle", synced);

        Assert.That(Has("b-optional-mod.jar"), Is.True);
        Assert.That(await new AutomaticModsStore(_storage).GetAsync("Waffle"), Is.EqualTo(new[] { "b-optional-mod.jar" }));

        // The sync downloads a fresh copy of a missing optional mod as disabled.
        File.Move(Path.Combine(_mods, "b-optional-mod.jar"), Path.Combine(_mods, "b-optional-mod.jar.disabled"));
        synced.OnNext(new FileSyncResult([], []));

        Assert.That(Has("b-optional-mod.jar"), Is.True);
    }

    [Test]
    public async Task Prepare_ApiDown_LeavesFilesAlone()
    {
        _details = null;
        Put("a-optional-mod.jar");
        Put("b-optional-mod.jar.disabled");

        using var _ = await Guard().PrepareAsync(_mods, "Waffle", new Subject<FileSyncResult>());

        Assert.That(Has("b-optional-mod.jar.disabled"), Is.True);
    }
}
