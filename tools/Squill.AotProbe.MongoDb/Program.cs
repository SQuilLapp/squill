using MongoDB.Bson;
using MongoDB.Driver;

// args[0] = connection string. BsonDocument only (no POCO class maps): insert, find, aggregate, list collections and indexes.
var client = new MongoClient(args[0]);
var db = client.GetDatabase("probe");
await db.DropCollectionAsync("items");
var col = db.GetCollection<BsonDocument>("items");
await col.InsertManyAsync(new[]
{
    new BsonDocument { { "name", "a" }, { "qty", 3 }, { "tags", new BsonArray { "x", "y" } }, { "meta", new BsonDocument("k", 1.5) } },
    new BsonDocument { { "name", "b" }, { "when", DateTime.UtcNow }, { "price", new BsonDecimal128(12.34m) } },
});
await col.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocument("name", 1)));
Console.WriteLine("server " + (await db.RunCommandAsync<BsonDocument>(new BsonDocument("buildInfo", 1)))["version"]);
foreach (var d in await (await col.FindAsync(BsonDocument.Parse("{ \"qty\": { \"$gte\": 1 } }"))).ToListAsync())
    Console.WriteLine("find " + d.ToJson());
var agg = await col.Aggregate().Group(BsonDocument.Parse("{ \"_id\": null, \"n\": { \"$sum\": 1 } }")).ToListAsync();
Console.WriteLine("agg " + agg[0].ToJson());
foreach (var c in await (await db.ListCollectionNamesAsync()).ToListAsync()) Console.WriteLine("collection " + c);
foreach (var i in await (await col.Indexes.ListAsync()).ToListAsync()) Console.WriteLine("index " + i.ToJson());
Console.WriteLine("OK");
