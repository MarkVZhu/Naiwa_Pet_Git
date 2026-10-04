using Naiwa.Content;

namespace Naiwa.Tests
{
    /// <summary>测试用内容配置（v1.0 §3.2 示例）。</summary>
    static class TestContent
    {
        public const string SampleJson = @"{
  ""schemaVersion"": 2,
  ""forms"": [
    { ""form"": ""Egg"", ""displayName"": ""奶蛋"", ""idle"": { ""folder"": ""egg_Idle"", ""fps"": 12 },
      ""emotes"": [
        { ""id"": ""egg_drink"", ""displayName"": ""干一杯"", ""folder"": ""egg_emo_drink"", ""fps"": 24, ""unlock"": ""Default"" },
        { ""id"": ""egg_bath"",  ""displayName"": ""泡个澡"", ""folder"": ""egg_emo_bath"",  ""fps"": 24, ""unlock"": ""Lottery"", ""lotteryWeight"": 1, ""icon"": ""icons/egg_bath.png"" } ] },
    { ""form"": ""Small"", ""displayName"": ""小奶蛙"", ""idle"": { ""folder"": ""small_idle"", ""fps"": 12 },
      ""emotes"": [
        { ""id"": ""small_armcross"", ""displayName"": ""抱臂摇摆"", ""folder"": ""small_armcross"", ""fps"": 24, ""unlock"": ""Default"" },
        { ""id"": ""small_heart"",    ""displayName"": ""比心"",     ""folder"": ""small_heart"",    ""fps"": 24, ""unlock"": ""Lottery"" } ] },
    { ""form"": ""Big"", ""displayName"": ""大奶蛙"", ""idle"": { ""folder"": ""big_Idle"", ""fps"": 12 },
      ""emotes"": [
        { ""id"": ""big_laugh"", ""displayName"": ""捧腹大笑"",   ""folder"": ""big_emo_laugh"", ""fps"": 24, ""unlock"": ""Default"" },
        { ""id"": ""big_fall"",  ""displayName"": ""摔个大跟头"", ""folder"": ""big_emo_fall"",  ""fps"": 24, ""unlock"": ""Lottery"" } ] }
  ]
}";

        public static ContentConfigResult Parse(string json = SampleJson) => ContentConfig.Parse(json, name => name);

        public static EmoteCatalog Catalog(string json = SampleJson) => new EmoteCatalog(Parse(json));
    }
}
