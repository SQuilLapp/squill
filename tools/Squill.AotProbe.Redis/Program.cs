using StackExchange.Redis;

// args[0] = host:port. One value of each type, SCAN, TYPE, raw command through Execute.
await using var mux = await ConnectionMultiplexer.ConnectAsync(args[0]);
var db = mux.GetDatabase();
var server = mux.GetServer(mux.GetEndPoints()[0]);
Console.WriteLine("server " + server.Version);
await db.StringSetAsync("p:str", "v");
await db.HashSetAsync("p:hash", new[] { new HashEntry("f1", "1"), new HashEntry("f2", "2") });
await db.ListRightPushAsync("p:list", new RedisValue[] { "a", "b" });
await db.SetAddAsync("p:set", new RedisValue[] { "m", "n" });
await db.SortedSetAddAsync("p:zset", "z", 1.5);
await db.StreamAddAsync("p:stream", "field", "value");
await foreach (var k in server.KeysAsync(pattern: "p:*"))
    Console.WriteLine($"key {k} {await db.KeyTypeAsync(k)} ttl={await db.KeyTimeToLiveAsync(k)}");
Console.WriteLine("hash " + string.Join(",", (await db.HashGetAllAsync("p:hash")).Select(e => $"{e.Name}={e.Value}")));
Console.WriteLine("zset " + string.Join(",", (await db.SortedSetRangeByRankWithScoresAsync("p:zset")).Select(e => $"{e.Element}:{e.Score}")));
Console.WriteLine("stream " + (await db.StreamRangeAsync("p:stream")).Length);
Console.WriteLine("raw " + await db.ExecuteAsync("INFO", "server") is { } r);
Console.WriteLine("raw SET " + await db.ExecuteAsync("SET", "p:raw", "1"));
Console.WriteLine("OK");
