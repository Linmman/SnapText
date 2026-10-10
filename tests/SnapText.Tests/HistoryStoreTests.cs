using System.IO;
using SnapText.Core;

namespace SnapText.Tests;

/// <summary>HistoryStore：计数轻量化路径与增删闭环（用临时历史文件，不碰真实数据）。</summary>
public class HistoryStoreTests : IDisposable
{
    private readonly string _path;

    public HistoryStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"snaptext-tests-{Guid.NewGuid():N}.jsonl");
        Environment.SetEnvironmentVariable("SNAPTEXT_HISTORY_FILE", _path);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SNAPTEXT_HISTORY_FILE", null);
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void Count_TracksAddClear()
    {
        var store = new HistoryStore();
        Assert.Equal(0, store.Count);

        Assert.True(store.Add("第一条", "ocr"));
        Assert.True(store.Add("第二条", "burst"));
        Assert.Equal(2, store.Count);

        // 与最近一条相同 → 跳过（不写入）
        Assert.False(store.Add("第二条", "burst"));
        Assert.Equal(2, store.Count);

        store.Clear();
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Count_AgreesWithLoad()
    {
        var store = new HistoryStore();
        for (var i = 0; i < 5; i++)
            store.Add($"第{i}条内容", "ocr");

        Assert.Equal(store.Load().Count, store.Count);
        Assert.Equal(5, store.Count);
    }

    [Fact]
    public void Count_ReturnsZeroForMissingFile()
    {
        Environment.SetEnvironmentVariable("SNAPTEXT_HISTORY_FILE",
            Path.Combine(Path.GetTempPath(), $"snaptext-missing-{Guid.NewGuid():N}.jsonl"));
        try
        {
            Assert.Equal(0, new HistoryStore().Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SNAPTEXT_HISTORY_FILE", _path);
        }
    }
}
