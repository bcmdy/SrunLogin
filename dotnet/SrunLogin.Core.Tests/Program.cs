using System.Text;
using SrunLogin.Crypto;
using SrunLogin.Services;
using SrunLogin.Utils;

var tests = new (string Name, Action Run)[]
{
    ("SrunBase64 与 Python 字母表一致", TestSrunBase64),
    ("XXTEA 与 Python 原版输出一致", TestXXTea),
    ("登录 info JSON 使用紧凑格式", TestLoginInfoJson),
    ("HMAC-MD5 与 Python 标准库一致", TestHmacMd5),
    ("QueryString 保持 Python urlencode 行为", TestQueryString),
    ("响应解析覆盖 JSONP/CSV/纯文本", TestParseResponse),
    ("HTML 提取 ac_id 与 IP", TestHtmlExtraction),
    ("格式化工具输出稳定", TestFormatters)
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"[PASS] {test.Name}");
}

Console.WriteLine($"All {tests.Length} tests passed.");

static void TestSrunBase64()
{
    var encoded = SrunBase64.Encode(Encoding.ASCII.GetBytes("hello"));
    AssertEqual("OCubWC4=", encoded);
}

static void TestXXTea()
{
    var info = SrunProtocol.BuildLoginInfoJson("alice@edu.cn", "secret", "10.0.0.8", "143");
    var encrypted = XXTea.Encrypt(info, "challenge-token");
    var actual = string.Join(",", encrypted.Select(c => ((int)c).ToString()));
    const string expected = "44,164,242,61,244,29,233,213,14,135,187,107,59,76,250,166,113,253,180,237,185,187,110,177,188,125,3,151,5,167,76,166,61,169,251,29,128,161,108,203,212,199,114,221,198,188,126,92,13,162,250,45,62,95,222,148,153,194,73,170,202,23,71,172,1,112,99,236,102,163,39,32,140,73,219,10,192,6,84,102,198,195,229,26,34,209,108,103,124,35,39,161,214,60,53,249,18,242,75,82,98,203,254,165";
    AssertEqual(expected, actual);
}

static void TestLoginInfoJson()
{
    var info = SrunProtocol.BuildLoginInfoJson("alice@edu.cn", "secret", "10.0.0.8", "143");
    AssertEqual("""{"username":"alice@edu.cn","password":"secret","ip":"10.0.0.8","acid":"143","enc_ver":"srun_bx1"}""", info);
}

static void TestHmacMd5()
{
    var actual = SrunProtocol.ComputeHmacMd5("challenge-token", "secret");
    AssertEqual("26d84ba8ed9b414dcf97bb1ca6ca4065", actual);
}

static void TestQueryString()
{
    var query = SrunProtocol.BuildQueryString([
        new("action", "login"),
        new("os", "Windows 10"),
        new("username", "alice@edu.cn"),
        new("callback", "jQuery123_456")
    ]);

    AssertEqual("action=login&os=Windows+10&username=alice%40edu.cn&callback=jQuery123_456", query);
}

static void TestParseResponse()
{
    var ok = SrunProtocol.ParseResponse("ok");
    AssertEqual("ok", ok.GetProperty("error").GetString());

    var challenge = SrunProtocol.ParseResponse("challenge=abc123");
    AssertEqual("abc123", challenge.GetProperty("challenge").GetString());

    var jsonp = SrunProtocol.ParseResponse("jQuery123({\"error\":\"ok\",\"online_ip\":\"10.0.0.8\"});");
    AssertEqual("10.0.0.8", jsonp.GetProperty("online_ip").GetString());

    var csv = SrunProtocol.ParseResponse("alice,1,2,3,3600,5,2048,7,10.0.0.8");
    AssertEqual("alice", csv.GetProperty("user_name").GetString());
    AssertEqual("10.0.0.8", csv.GetProperty("online_ip").GetString());
    AssertEqual(2048L, csv.GetProperty("sum_bytes").GetInt64());
    AssertEqual(3600L, csv.GetProperty("sum_seconds").GetInt64());
}

static void TestHtmlExtraction()
{
    const string html = """
        <html>
          <input id="ac_id" value="1" />
          <script>
            var ac_id = '143';
            var ip = '10.0.0.8';
          </script>
          <a href="/srun_portal_pc?ac_id=143">portal</a>
        </html>
        """;

    AssertEqual("143", SrunProtocol.ExtractAcId(html));
    AssertEqual("10.0.0.8", SrunProtocol.ExtractIp(html));
}

static void TestFormatters()
{
    AssertEqual("2.00 KB", Formatters.FormatFlow(2048));
    AssertEqual("1小时1分1秒", Formatters.FormatTime(3661));
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected: {expected}; Actual: {actual}");
}

