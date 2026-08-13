using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;
using Driftya.SpriteAtlasForge.Infrastructure;

namespace Driftya.SpriteAtlasForge.Infrastructure.Tests;

public sealed class NativeAtlasProjectStoreTests
{
    [Test]
    public async Task Native_format_round_trips_connectors_tags_and_properties()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("atlas.saf.json");
        var store = new NativeAtlasProjectStore();
        var project = CreateProject();

        await store.SaveAsync(project, path);
        var loaded = await store.LoadAsync(path);

        await Assert.That(loaded.FormatVersion).IsEqualTo(1);
        await Assert.That(loaded.Sprites).Count().IsEqualTo(1);
        await Assert.That(loaded.Sprites[0].Connectors[1].Name).IsEqualTo("next");
        await Assert.That(loaded.Sprites[0].Connectors[1].X).IsEqualTo(31);
        await Assert.That(loaded.Sprites[0].Properties["weight"].Value).IsEqualTo("1.25");
    }

    [Test]
    public async Task Native_format_writes_deterministic_JSON()
    {
        using var directory = new TestDirectory();
        var firstPath = directory.GetPath("first.saf.json");
        var secondPath = directory.GetPath("second.saf.json");
        var store = new NativeAtlasProjectStore();
        var project = CreateProject();

        await store.SaveAsync(project, firstPath);
        await store.SaveAsync(project, secondPath);

        await Assert.That(await File.ReadAllTextAsync(firstPath))
            .IsEqualTo(await File.ReadAllTextAsync(secondPath));
    }

    [Test]
    public async Task Native_format_matches_the_v1_golden_fixture()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("atlas.saf.json");
        var store = new NativeAtlasProjectStore();
        await store.SaveAsync(CreateProject(), path);
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-v1.golden.saf.json");

        var actual = JsonNode.Parse(await File.ReadAllTextAsync(path));
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(expectedPath));

        await Assert.That(JsonNode.DeepEquals(actual, expected)).IsTrue();
    }

    [Test]
    public async Task Native_format_tolerates_unknown_additive_fields()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("atlas.saf.json");
        var store = new NativeAtlasProjectStore();
        await store.SaveAsync(CreateProject(), path);
        var json = await File.ReadAllTextAsync(path);
        json = json.Replace("\"formatVersion\": 1,", "\"formatVersion\": 1,\n  \"futureField\": true,");
        await File.WriteAllTextAsync(path, json);

        var loaded = await store.LoadAsync(path);

        await Assert.That(loaded.Name).IsEqualTo("modules");
    }

    [Test]
    public async Task Native_format_reports_unsupported_versions()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("atlas.saf.json");
        await File.WriteAllTextAsync(path, """
            {
              "formatVersion": 99,
              "name": "future",
              "source": {},
              "atlas": {},
              "sprites": []
            }
            """);
        var store = new NativeAtlasProjectStore();

        var exception = await Assert.ThrowsAsync<AtlasProjectFormatException>(() => store.LoadAsync(path));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Path).IsEqualTo("formatVersion");
    }

    [Test]
    public async Task Native_format_reports_malformed_JSON_with_a_path_aware_exception()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("atlas.saf.json");
        await File.WriteAllTextAsync(path, "{ \"formatVersion\": 1, \"sprites\": [ }");
        var store = new NativeAtlasProjectStore();

        var exception = await Assert.ThrowsAsync<AtlasProjectFormatException>(() => store.LoadAsync(path));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("Invalid native atlas JSON");
    }

    private static AtlasProject CreateProject()
    {
        var region = new PixelRect(12, 8, 32, 16);
        var sprite = new AtlasSprite(
            "habitat_03",
            region,
            region,
            [new AtlasConnector("anchor", 0, 8), new AtlasConnector("next", 31, 8)],
            ["population", "habitat"],
            new Dictionary<string, AtlasPropertyValue>
            {
                ["position"] = AtlasPropertyValue.FromString("Middle"),
                ["weight"] = AtlasPropertyValue.FromNumber(1.25m),
            });
        return new AtlasProject(
            "modules",
            new AtlasSource("source/modules.png", new PixelSize(128, 64), new string('b', 64)),
            new AtlasOutput("source/modules.png", new PixelSize(128, 64), false),
            [sprite]);
    }
}
