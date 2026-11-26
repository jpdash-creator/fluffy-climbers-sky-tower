using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace FluffyClimbers.LeaderboardService.Tests
{
    /// <summary>PUT / GET / DELETE /v1/save (full save backup copy, Final Bugs 1 item 12): per-uid storage, validation, rate limits, isolation between players.</summary>
    public class SaveBackupTests
    {
        static dynamic Body(ApiResult r) { return JObject.FromObject(r.Body); }

        static byte[] Gzip(string text)
        {
            using (var ms = new MemoryStream())
            {
                using (var z = new GZipStream(ms, CompressionLevel.Fastest, true)) { byte[] b = Encoding.UTF8.GetBytes(text); z.Write(b, 0, b.Length); }
                return ms.ToArray();
            }
        }

        const string Envelope = "{\"format\":1,\"savedUtc\":\"2026-11-26T10:00:00Z\",\"saveVersion\":30,\"summary\":{\"levels\":3},\"data\":{\"gems\":42,\"coins\":1000}}";

        static SaveBackupApi.PutRequest Req(string json = Envelope, int format = 1, int version = 30)
        {
            byte[] gz = Gzip(json);
            return new SaveBackupApi.PutRequest { Format = format, SaveVersion = version, SavedUtc = "2026-11-26T10:00:00Z", Gz = Convert.ToBase64String(gz), Sha256 = SaveBackupApi.HexSha256(gz) };
        }

        sealed class Rig
        {
            public InMemorySaveBackupStore Store = new InMemorySaveBackupStore();
            public DateTime Now = new DateTime(2026, 11, 26, 9, 0, 0, DateTimeKind.Utc);
            public SaveBackupApi Api;
            public Rig() { Api = new SaveBackupApi(Store, () => Now); }
            public void Wait(int seconds) { Now = Now.AddSeconds(seconds); }
        }

        [Fact]
        public async Task PutThenGetRoundTripsTheExactBytes()
        {
            var rig = new Rig();
            SaveBackupApi.PutRequest req = Req();
            ApiResult put = await rig.Api.PutAsync("alice", req);
            Assert.Equal(200, put.Status);
            Assert.True((bool)Body(put).saved);
            ApiResult get = await rig.Api.GetAsync("alice");
            Assert.Equal(200, get.Status);
            dynamic g = Body(get);
            Assert.Equal(req.Gz, (string)g.gz);
            Assert.Equal(req.Sha256, (string)g.sha256);
            Assert.Equal(1, (int)g.format);
            Assert.Equal(30, (int)g.saveVersion);
        }

        [Fact]
        public async Task GetWithoutABackupIs404()
        {
            var rig = new Rig();
            ApiResult r = await rig.Api.GetAsync("alice");
            Assert.Equal(404, r.Status);
            Assert.Equal("no_backup", (string)Body(r).error);
        }

        [Fact]
        public async Task PlayersNeverSeeEachOthersBackup()
        {
            var rig = new Rig();
            Assert.Equal(200, (await rig.Api.PutAsync("alice", Req())).Status);
            Assert.Equal(404, (await rig.Api.GetAsync("bob")).Status);
            Assert.Equal(200, (await rig.Api.PutAsync("bob", Req(Envelope.Replace("42", "7")))).Status);
            Assert.Equal(2, rig.Store.Count);
            // deleting bob's copy leaves alice's alone
            Assert.True((bool)Body(await rig.Api.DeleteAsync("bob")).deleted);
            Assert.Equal(200, (await rig.Api.GetAsync("alice")).Status);
        }

        [Fact]
        public async Task ASecondPutReplacesTheFirst()
        {
            var rig = new Rig();
            await rig.Api.PutAsync("alice", Req());
            rig.Wait(10);
            SaveBackupApi.PutRequest second = Req(Envelope.Replace("42", "99"));
            Assert.Equal(200, (await rig.Api.PutAsync("alice", second)).Status);
            Assert.Equal(second.Gz, (string)Body(await Settle(rig, "alice")).gz);
            Assert.Equal(1, rig.Store.Count);
        }

        static async Task<ApiResult> Settle(Rig rig, string uid) { rig.Wait(5); return await rig.Api.GetAsync(uid); }

        [Fact]
        public async Task PutIsRateLimitedPerPlayer()
        {
            var rig = new Rig();
            Assert.Equal(200, (await rig.Api.PutAsync("alice", Req())).Status);
            Assert.Equal(429, (await rig.Api.PutAsync("alice", Req())).Status);
            Assert.Equal(200, (await rig.Api.PutAsync("bob", Req())).Status);   // another player is not affected
            rig.Wait(6);
            Assert.Equal(200, (await rig.Api.PutAsync("alice", Req())).Status);
        }

        [Fact]
        public async Task BadChecksumBadBase64AndNonSaveBytesAreRefused()
        {
            var rig = new Rig();
            SaveBackupApi.PutRequest r = Req();
            r.Sha256 = new string('0', 64);
            Assert.Equal(422, (await rig.Api.PutAsync("a", r)).Status);
            rig.Wait(6);
            Assert.Equal(400, (await rig.Api.PutAsync("a", new SaveBackupApi.PutRequest { Format = 1, SaveVersion = 30, Gz = "***not base64***", Sha256 = "x" })).Status);
            rig.Wait(6);
            byte[] plain = Encoding.UTF8.GetBytes(Envelope);   // not gzip
            Assert.Equal(422, (await rig.Api.PutAsync("a", new SaveBackupApi.PutRequest { Format = 1, SaveVersion = 30, Gz = Convert.ToBase64String(plain), Sha256 = SaveBackupApi.HexSha256(plain) })).Status);
            rig.Wait(6);
            Assert.Equal(422, (await rig.Api.PutAsync("a", Req("{\"hello\":1}"))).Status);   // gzip, but no envelope with data
            rig.Wait(6);
            Assert.Equal(422, (await rig.Api.PutAsync("a", Req("not json at all"))).Status);
            Assert.Equal(0, rig.Store.Count);
        }

        [Fact]
        public async Task UnknownFormatAndMissingFieldsAreRefused()
        {
            var rig = new Rig();
            Assert.Equal(426, (await rig.Api.PutAsync("a", Req(format: 2))).Status);
            rig.Wait(6);
            Assert.Equal(400, (await rig.Api.PutAsync("a", Req(version: 0))).Status);
            rig.Wait(6);
            Assert.Equal(400, (await rig.Api.PutAsync("a", null)).Status);
            rig.Wait(6);
            Assert.Equal(400, (await rig.Api.PutAsync("a", new SaveBackupApi.PutRequest { Format = 1, SaveVersion = 1 })).Status);
            Assert.Equal(401, (await rig.Api.PutAsync("", Req())).Status);
            Assert.Equal(401, (await rig.Api.GetAsync(null)).Status);
            Assert.Equal(401, (await rig.Api.DeleteAsync("")).Status);
        }

        [Fact]
        public async Task OversizedUploadsAreRefused()
        {
            var rig = new Rig();
            var rng = new Random(5);
            var raw = new byte[SaveBackupApi.MaxGzBytes + 10];   // incompressible
            rng.NextBytes(raw);
            var big = new SaveBackupApi.PutRequest { Format = 1, SaveVersion = 30, Gz = Convert.ToBase64String(raw), Sha256 = SaveBackupApi.HexSha256(raw) };
            Assert.Equal(413, (await rig.Api.PutAsync("a", big)).Status);
            Assert.Equal(0, rig.Store.Count);
        }

        [Fact]
        public async Task ZipBombIsRefusedAfterTheUnpackLimit()
        {
            var rig = new Rig();
            var sb = new StringBuilder("{\"data\":{\"x\":\"");
            sb.Append('a', SaveBackupApi.MaxJsonBytes + 1024);
            sb.Append("\"}}");
            SaveBackupApi.PutRequest bomb = Req(sb.ToString());
            Assert.True(Convert.FromBase64String(bomb.Gz).Length < SaveBackupApi.MaxGzBytes);   // tiny on the wire
            Assert.Equal(422, (await rig.Api.PutAsync("a", bomb)).Status);
        }

        [Fact]
        public async Task DeleteIsIdempotentAndRemovesTheBackup()
        {
            var rig = new Rig();
            await rig.Api.PutAsync("alice", Req());
            Assert.True((bool)Body(await rig.Api.DeleteAsync("alice")).deleted);
            Assert.False((bool)Body(await rig.Api.DeleteAsync("alice")).deleted);
            rig.Wait(3);
            Assert.Equal(404, (await rig.Api.GetAsync("alice")).Status);
        }

        [Fact]
        public async Task GetIsRateLimitedAndStoredDocumentCarriesRetention()
        {
            var rig = new Rig();
            await rig.Api.PutAsync("alice", Req());
            Assert.Equal(200, (await rig.Api.GetAsync("alice")).Status);
            Assert.Equal(429, (await rig.Api.GetAsync("alice")).Status);
            SaveBackupDoc d = await rig.Store.GetAsync("alice");
            Assert.Equal(rig.Now.AddDays(SaveBackupApi.RetentionDays), d.ExpireAt);
            Assert.Equal(rig.Now, d.UpdatedAt);
            Assert.Equal(d.Gz.Length, d.Size);
        }
    }
}
