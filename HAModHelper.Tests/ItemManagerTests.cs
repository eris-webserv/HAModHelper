using System;
using HAModHelper.GamePlugin.Items.Interfaces;
using HAModHelper.GamePlugin.Items.Systems;
using Xunit;

namespace HAModHelper.Tests
{
    // we reference internal helpers from the library via InternalsVisibleTo
    public class ItemManagerTests
    {
        private class FakeResourceControl : IResourceControl
        {
            public Dictionary<string, Dictionary<string, string>> loaded_inventory_item_files { get; set; }
                = new Dictionary<string, Dictionary<string, string>>();
        }

        public ItemManagerTests()
        {
            // clear singleton state so tests don't leak into one another
            ItemManager.Instance.Reset();
        }

        [Fact]
        public void AddAndRetrieveItem()
        {
            var im = ItemManager.Instance;
            im.DebugResourceControlSource = new FakeResourceControl();

            var item = new Item { ModId = "mod", ItemId = "foo", Name = "Test" };
            im.AddItem(item);

            var result = im.GetItem("mod:foo");
            Assert.Same(item, result);
        }

        [Fact]
        public void DeleteBaseItemBlocksAndRemoves()
        {
            var im = ItemManager.Instance;
            im.DebugResourceControlSource = new FakeResourceControl();
            var item = new Item { ModId = "base", ItemId = "bar", Name = "Bar" };
            im.AddItem(item);
            im.DeleteItem(item);

            Assert.Null(im.GetItem("base:bar"));
            Assert.True(im.IsBaseItemBlocked("bar"));
        }

        [Fact]
        public void GetItemFallsBackToGameFields()
        {
            var im = ItemManager.Instance;
            var fake = new FakeResourceControl();
            im.DebugResourceControlSource = fake;

            var fields = new Dictionary<string, string>();
            fields["Name"] = "FromGame";
            fake.loaded_inventory_item_files["someid"] = fields;

            var item = im.GetItem("someid");
            Assert.NotNull(item);
            Assert.Equal("someid", item.ItemId);
            Assert.Equal("FromGame", item.Name);
        }

        [Fact]
        public void QueuedItemsAreProcessedWhenResourceControlBecomesAvailable()
        {
            var im = ItemManager.Instance;
            im.DebugResourceControlSource = new DebugNoLoadResourceControl();
            var item = new Item { ModId = "mod", ItemId = "queued", Name = "Q" };
            im.AddItem(item);

            var fake = new FakeResourceControl();
            im.DebugResourceControlSource = fake;
            im.ProcessQueuedItems();

            Assert.True(fake.loaded_inventory_item_files.ContainsKey(item.Id));
        }

        [Fact]
        public void ItemConverterRoundtripsCorrectly()
        {
            var original = new Item
            {
                ModId = "m",
                ItemId = "i",
                Name = "name",
                Description = "desc",
                StackLimit = 5,
                SpritePath = "spr",
                ExtraFields = { ["key"] = "val" }
            };

            var fields = ItemConverter.ToGameFields(original);
            var round = ItemConverter.FromGameFields("m:i", fields);

            Assert.Equal(original.ModId, round.ModId);
            Assert.Equal(original.ItemId, round.ItemId);
            Assert.Equal(original.Name, round.Name);
            Assert.Equal(original.Description, round.Description);
            Assert.Equal(original.StackLimit, round.StackLimit);
            Assert.Equal(original.SpritePath, round.SpritePath);
            Assert.Equal("val", round.ExtraFields["key"]);
        }
    }
}
